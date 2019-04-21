using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;

using ECSMesh;
using MapGeneration;
using Unity.Rendering;

[AlwaysUpdateSystem]
[UpdateBefore(typeof(TransformSystemGroup))]
public class TerrainMeshDataSystem : ComponentSystem
{
    EntityManager entityManager;

    EntityQuery meshDataGroup;

    TopologyUtil topologyUtil;

    ASyncJobManager jobManager;

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;

        topologyUtil = new TopologyUtil().Construct();

        EntityQueryDesc meshDataQuery = new EntityQueryDesc{
            All = new ComponentType[] { typeof(Tags.TerrainEntity), typeof(WorleyNoise.PointData), typeof(TopologySystem.Height) },
            None = new ComponentType[] { typeof(Vertex) }
        };
        meshDataGroup = GetEntityQuery(meshDataQuery);
    }

    protected override void OnDestroy()
    {
        jobManager.Dispose();
    }

    protected override void OnUpdate()
    {
        if(!jobManager.AllJobsCompleted())
            return;
        else
            ScheduleMeshDataJobs();
    }

    void ScheduleMeshDataJobs()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        NativeArray<ArchetypeChunk> chunks = meshDataGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        var entityType = GetArchetypeChunkEntityType();
        var matrixType = GetArchetypeChunkComponentType<CellSystem.MatrixComponent>(true);
        var sectorTypeType = GetArchetypeChunkComponentType<SectorSystem.TypeComponent>(true);
        var masterCellType = GetArchetypeChunkComponentType<SectorSystem.MasterCell>(true);

        var worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);
        var topologyType = GetArchetypeChunkBufferType<TopologySystem.Height>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<CellSystem.MatrixComponent> matrices = chunk.GetNativeArray(matrixType);
            NativeArray<SectorSystem.TypeComponent> sectorTypes = chunk.GetNativeArray(sectorTypeType);
            NativeArray<SectorSystem.MasterCell> masterCells = chunk.GetNativeArray(masterCellType);

            BufferAccessor<WorleyNoise.PointData> worleyArrays = chunk.GetBufferAccessor(worleyType);
            BufferAccessor<TopologySystem.Height> topologyArrays = chunk.GetBufferAccessor(topologyType);

            for(int e = 0; e < entities.Length; e++)
            {
                var worley = new NativeArray<WorleyNoise.PointData>(worleyArrays[e].AsNativeArray(), Allocator.TempJob);
                var height = new NativeArray<TopologySystem.Height>(topologyArrays[e].AsNativeArray(), Allocator.TempJob);

                TerrainMeshDataJob job = new TerrainMeshDataJob{
                    commandBuffer = jobManager.commandBuffer,
                    sectorEntity = entities[e],
                    sectorType = sectorTypes[e].Value,
                    matrix = matrices[e],
                    masterCell = masterCells[e].Value,
                    worley = worley,
                    terrainHeightArray = height,
                    topologyUtil = topologyUtil
                }; 
                
                jobManager.ScheduleNewJob(job);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
        chunks.Dispose();
    }
}
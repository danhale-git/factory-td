using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;

using ECSMesh;
using MapGeneration;

public class MeshDataSystem : ComponentSystem
{
    EntityManager entityManager;

    EntityQuery meshDataGroup;

    TopologyUtil biomes;

    ASyncJobManager jobManager;

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;

        biomes = new TopologyUtil();

        EntityQueryDesc meshDataQuery = new EntityQueryDesc{
            All = new ComponentType[] { typeof(WorleyNoise.CellData), typeof(TopologySystem.Height) },
            None = new ComponentType[] { typeof(Unity.Rendering.RenderMesh), typeof(Vertex) }
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
        NativeArray<ArchetypeChunk> chunks = meshDataGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        var entityType = GetArchetypeChunkEntityType();
        var matrixType = GetArchetypeChunkComponentType<CellSystem.MatrixComponent>(true);
        var sectorTypeType = GetArchetypeChunkComponentType<SectorSystem.TypeComponent>(true);
        var sectorGroupingType = GetArchetypeChunkComponentType<SectorSystem.SectorGrouping>(true);
        var sectorMasterCellType = GetArchetypeChunkComponentType<SectorSystem.MasterCell>(true);

        var worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);
        var topologyType = GetArchetypeChunkBufferType<TopologySystem.Height>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<CellSystem.MatrixComponent> matrices = chunk.GetNativeArray(matrixType);
            NativeArray<SectorSystem.TypeComponent> sectorTypes = chunk.GetNativeArray(sectorTypeType);
            NativeArray<SectorSystem.SectorGrouping> sectorGroupings = chunk.GetNativeArray(sectorGroupingType);
            NativeArray<SectorSystem.MasterCell> sectorMasterCells = chunk.GetNativeArray(sectorMasterCellType);

            BufferAccessor<WorleyNoise.PointData> worleyArrays = chunk.GetBufferAccessor(worleyType);
            BufferAccessor<TopologySystem.Height> topologyArrays = chunk.GetBufferAccessor(topologyType);

            for(int e = 0; e < entities.Length; e++)
            {
                var worley = new NativeArray<WorleyNoise.PointData>(topologyArrays[e].Length, Allocator.TempJob);
                var height = new NativeArray<TopologySystem.Height>(topologyArrays[e].Length, Allocator.TempJob);

                worley.CopyFrom(worleyArrays[e].AsNativeArray());
                height.CopyFrom(topologyArrays[e].AsNativeArray());

                TerrainMeshDataJob job = new TerrainMeshDataJob{
                    commandBuffer = jobManager.commandBuffer,
                    sectorEntity = entities[e],
                    sectorType = sectorTypes[e].Value,
                    matrix = matrices[e],
                    worley = worley,
                    sectorGrouping = sectorGroupings[e].Value,
                    pointHeight = height,
                    arrayUtil = new ArrayUtil()
                }; 
                
                jobManager.ScheduleNewJob(job);
            }
        }

        chunks.Dispose();
    }
}
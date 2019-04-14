using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;

using ECSMesh;
using MapGeneration;
using Unity.Rendering;

[UpdateBefore(typeof(TransformSystemGroup))]
public class TerrainMeshDataSystem : ComponentSystem
{
    EntityManager entityManager;

    EntityQuery meshDataGroup;
    EntityArchetype waterArchetype;

    TopologyUtil biomes;

    ASyncJobManager jobManager;
    ASyncJobManager waterJobManager;

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;

        biomes = new TopologyUtil();

        waterArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<LocalToWorld>(),
            ComponentType.ReadWrite<Translation>(),
            ComponentType.ReadWrite<RenderMeshProxy>(),
            ComponentType.ReadWrite<Tags.WaterEntity>()
        );

        EntityQueryDesc meshDataQuery = new EntityQueryDesc{
            All = new ComponentType[] { typeof(Tags.TerrainEntity), typeof(WorleyNoise.CellData), typeof(TopologySystem.Height) },
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
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
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
                SectorSystem.SectorTypes sectorType = sectorTypes[e].Value;
                CellSystem.MatrixComponent matrix = matrices[e];
                WorleyNoise.CellData masterCell = sectorMasterCells[e].Value;

                var worley = new NativeArray<WorleyNoise.PointData>(worleyArrays[e].Length, Allocator.Persistent);
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
                    pointHeight = height
                }; 
                
                jobManager.ScheduleNewJob(job);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
        chunks.Dispose();
    }
}
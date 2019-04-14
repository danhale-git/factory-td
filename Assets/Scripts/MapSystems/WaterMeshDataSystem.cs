using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;

using ECSMesh;
using MapGeneration;
using Unity.Rendering;

namespace Tags
{
    public struct WaterEntity : IComponentData { }
}

[AlwaysUpdateSystem]
[UpdateBefore(typeof(TransformSystemGroup))]
public class WaterMeshDataSystem : ComponentSystem
{
    EntityManager entityManager;

    EntityQuery meshDataGroup;
    EntityArchetype waterArchetype;

    TopologyUtil biomes;

    ASyncJobManager jobManager;

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
            All = new ComponentType[] { typeof(Tags.TerrainEntity), typeof(Tags.CreateWaterEntity), typeof(WorleyNoise.CellData), typeof(TopologySystem.Height) },
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
        var sectorMasterCellType = GetArchetypeChunkComponentType<SectorSystem.MasterCell>(true);
        var worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<CellSystem.MatrixComponent> matrices = chunk.GetNativeArray(matrixType);
            NativeArray<SectorSystem.MasterCell> sectorMasterCells = chunk.GetNativeArray(sectorMasterCellType);
            BufferAccessor<WorleyNoise.PointData> worleyArrays = chunk.GetBufferAccessor(worleyType);

            for(int e = 0; e < entities.Length; e++)
            {
                CellSystem.MatrixComponent matrix = matrices[e];
                WorleyNoise.CellData masterCell = sectorMasterCells[e].Value;
                Debug.Log(matrix.root);
                var worley = new NativeArray<WorleyNoise.PointData>(worleyArrays[e].AsNativeArray(), Allocator.Persistent);

                WaterMeshDataJob waterJob = new WaterMeshDataJob{
                    commandBuffer = jobManager.commandBuffer,
                    waterEntityArchetype = waterArchetype,
                    matrix = matrix,
                    masterCell = masterCell,
                    worley = worley
                };

                jobManager.ScheduleNewJob(waterJob);

                commandBuffer.RemoveComponent<Tags.CreateWaterEntity>(entities[e]);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
        chunks.Dispose();
    }
}
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

    TopologyUtil topologyUtil;

    ASyncJobManager jobManager;

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;

        topologyUtil = new TopologyUtil().Construct();

        waterArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<LocalToWorld>(),
            ComponentType.ReadWrite<Translation>(),
            ComponentType.ReadWrite<RenderMeshProxy>(),
            ComponentType.ReadWrite<Tags.WaterEntity>()
        );

        EntityQueryDesc meshDataQuery = new EntityQueryDesc{
            All = new ComponentType[] { typeof(Tags.TerrainEntity), typeof(Tags.CreateWaterEntity), typeof(TopologySystem.Height) },
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
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        var chunks = meshDataGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        var entityType = GetArchetypeChunkEntityType();
        var matrixType = GetArchetypeChunkComponentType<CellSystem.MatrixComponent>(true);
        var sectorMasterCellType = GetArchetypeChunkComponentType<SectorSystem.MasterCell>(true);
        var worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            var chunk = chunks[c];

            var entities = chunk.GetNativeArray(entityType);
            var matrices = chunk.GetNativeArray(matrixType);
            var sectorMasterCells = chunk.GetNativeArray(sectorMasterCellType);
            var worleyArrays = chunk.GetBufferAccessor(worleyType);

            for(int e = 0; e < entities.Length; e++)
            {
                CellSystem.MatrixComponent matrix = matrices[e];
                WorleyNoise.CellData masterCell = sectorMasterCells[e].Value;

                var worley = new NativeArray<WorleyNoise.PointData>(worleyArrays[e].AsNativeArray(), Allocator.Persistent);

                WaterMeshDataJob waterJob = new WaterMeshDataJob{
                    commandBuffer = jobManager.commandBuffer,
                    waterEntityArchetype = waterArchetype,
                    matrix = matrix,
                    masterCell = masterCell,
                    worley = worley,
                    topologyUtil = topologyUtil
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
using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;

public class TopologySystem : ComponentSystem
{
    EntityManager entityManager;

    ComponentGroup topologyGroup;

    public struct Topology : IBufferElementData
    {
        public float height;
    }

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        EntityArchetypeQuery topologyQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(WorleyNoise.CellData) },
            None = new ComponentType[] { typeof(WorleyCellSystem.CellComplete), typeof(Topology) }
        };
        topologyGroup = GetComponentGroup(topologyQuery);
    }

    protected override void OnUpdate()
    {
        ScheduleTopologyJobs();
    }

    void ScheduleTopologyJobs()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);

        NativeArray<ArchetypeChunk> chunks = topologyGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkBufferType<WorleyNoise.PointData> worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            BufferAccessor<WorleyNoise.PointData> worleyBuffers = chunk.GetBufferAccessor(worleyType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                DynamicBuffer<WorleyNoise.PointData> worley = worleyBuffers[e];

                DynamicBuffer<Topology> topologyBuffer = commandBuffer.AddBuffer<Topology>(entity);
                topologyBuffer.ResizeUninitialized(worley.Length);

                for(int i = 0; i < topologyBuffer.Length; i++)
                {
                    topologyBuffer[i] = new Topology{ height = /*worley[i].currentCellValue * 5 */0 };
                }
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }
}
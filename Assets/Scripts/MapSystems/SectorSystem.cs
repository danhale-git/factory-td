using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

public class SectorSystem : ComponentSystem
{
    EntityManager entityManager;

    public enum SectorTypes { NONE, UNPATHABLE }
    public struct SectorType : IComponentData { public SectorTypes Value; }
    
    [InternalBufferCapacity(0)]
    public struct Cell : IBufferElementData
    {
        public WorleyNoise.CellData data;
        public Entity entity;
    }

    public struct SectorValue : IComponentData
    {
        public float Value;
    }

    ComponentGroup sectorGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        EntityArchetypeQuery sectorQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(Cell) },
            None = new ComponentType[] { typeof(SectorType) }
        };
        sectorGroup = GetComponentGroup(sectorQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        NativeArray<ArchetypeChunk> chunks = sectorGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkBufferType<Cell> cellBufferType = GetArchetypeChunkBufferType<Cell>();

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];
            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            BufferAccessor<Cell> cellBuffers = chunk.GetBufferAccessor(cellBufferType);

            for(int e = 0; e < entities.Length; e++)
            {
                DynamicBuffer<Cell> cellBuffer = cellBuffers[e];

                float value = 0;
                for(int i = 0; i < cellBuffer.Length; i++)
                {
                    value += cellBuffer[i].data.value;
                }
                value /= cellBuffer.Length;

                Debug.Log(value);

                for(int i = 0; i < cellBuffer.Length; i++)
                {
                    commandBuffer.AddComponent<SectorValue>(cellBuffer[i].entity, new SectorValue{ Value = value });
                }

                commandBuffer.AddComponent(entities[e], new SectorType());
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }
}

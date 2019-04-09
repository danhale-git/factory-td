using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

public class SectorSystem : ComponentSystem
{
    EntityManager entityManager;

    Biomes biomes;

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

        biomes = new Biomes();

        EntityArchetypeQuery sectorQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(Cell) },
            None = new ComponentType[] { typeof(SectorType) }
        };
        sectorGroup = GetComponentGroup(sectorQuery);
    }

    protected override void OnUpdate()
    {
        CheckWorleyGeneration();
    }

    void CheckWorleyGeneration()
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

                if(!AllEntitiesHaveWorley(cellBuffer))
                {
                    continue;
                }

                float value = GetSectorValue(cellBuffer);

                SectorType type = new SectorType{ Value = SectorTypes.NONE };

                if(!SectorIsPathable(cellBuffer))
                    type.Value = SectorTypes.UNPATHABLE;

                AddSectorComponents(value, type, cellBuffer, commandBuffer);                

                commandBuffer.AddComponent(entities[e], type);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    bool AllEntitiesHaveWorley(DynamicBuffer<Cell> cellBuffer)
    {
        for(int i = 0; i < cellBuffer.Length; i++)
        {
            if( !entityManager.HasComponent(cellBuffer[i].entity, typeof(WorleyNoise.PointData)) )
                return false;
        }
        return true;
    }

    float GetSectorValue(DynamicBuffer<Cell> cellBuffer)
    {
        float value = 0;
        for(int i = 0; i < cellBuffer.Length; i++)
            value += cellBuffer[i].data.value;

        return value / cellBuffer.Length;
    }

    void AddSectorComponents(float value, SectorType type, DynamicBuffer<Cell> cellBuffer, EntityCommandBuffer commandBuffer)
    {
        for(int i = 0; i < cellBuffer.Length; i++)
        {
            commandBuffer.AddComponent<SectorValue>(cellBuffer[i].entity, new SectorValue{ Value = value });
            commandBuffer.AddComponent<SectorType>(cellBuffer[i].entity, type);
        }
    }

    bool SectorIsPathable(DynamicBuffer<Cell> cellBuffer)
    {
        for(int i = 0; i < cellBuffer.Length; i++)
        {
            DynamicBuffer<WorleyNoise.PointData> points = entityManager.GetBuffer<WorleyNoise.PointData>(cellBuffer[i].entity);
            for(int p = 0; p < points.Length; p++)
            {
                WorleyNoise.PointData point = points[p];

                float currentCellGroup = biomes.CellGrouping(point.currentCellIndex);
                float adjacentCellGroup = biomes.CellGrouping(point.adjacentCellIndex);

                if(currentCellGroup == adjacentCellGroup) continue;

                float currentCellHeight = biomes.CellHeight(point.currentCellIndex);
                float adjacentCellHeight = biomes.CellHeight(point.adjacentCellIndex);

                if(currentCellHeight == adjacentCellHeight) return true;

                int2 adjacentDirection = point.adjacentCellIndex - point.currentCellIndex;

                if(biomes.EdgeIsSloped(adjacentDirection, point.currentCellValue, point.adjacentCellValue))
                    return true;
            }
        }
        return false;
    }
}

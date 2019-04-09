using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

public class SectorSystem : ComponentSystem
{
    EntityManager entityManager;

    TopologyUtil topology;

    ComponentGroup sectorGroup;

    public enum SectorTypes { NONE, UNPATHABLE }

    public struct SectorType : IComponentData
    {
        public SectorTypes Value;
    }

    public struct SectorNoiseValue : IComponentData
    {
        public float Value;
    }
    
    [InternalBufferCapacity(0)]
    public struct Cell : IBufferElementData
    {
        public WorleyNoise.CellData data;
        public Entity entity;
    }

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        topology = new TopologyUtil();

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
        ArchetypeChunkBufferType<Cell> cellArrayType = GetArchetypeChunkBufferType<Cell>();

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];
            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            BufferAccessor<Cell> cellArrays = chunk.GetBufferAccessor(cellArrayType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity sectorEntity = entities[e];
                DynamicBuffer<Cell> cells = cellArrays[e];

                if(!AllEntitiesHaveWorley(cells))
                    continue;

                float value = GetSectorValue(cells);

                SectorType type = new SectorType();

                if(!SectorIsPathable(cells))
                    type.Value = SectorTypes.UNPATHABLE;

                AddSectorComponentsToCells(value, type, cells, commandBuffer);                

                commandBuffer.AddComponent(sectorEntity, type);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    bool AllEntitiesHaveWorley(DynamicBuffer<Cell> cellBuffer)
    {
        for(int i = 0; i < cellBuffer.Length; i++)
            if( !entityManager.HasComponent(cellBuffer[i].entity, typeof(WorleyNoise.PointData)) )
                return false;

        return true;
    }

    float GetSectorValue(DynamicBuffer<Cell> cellBuffer)
    {
        float value = 0;
        for(int i = 0; i < cellBuffer.Length; i++)
            value += cellBuffer[i].data.value;

        return value / cellBuffer.Length;
    }

    void AddSectorComponentsToCells(float value, SectorType type, DynamicBuffer<Cell> cellBuffer, EntityCommandBuffer commandBuffer)
    {
        for(int i = 0; i < cellBuffer.Length; i++)
        {
            commandBuffer.AddComponent<SectorNoiseValue>(cellBuffer[i].entity, new SectorNoiseValue{ Value = value });
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

                if(point.isSet == 0) continue;
                if(AdjacentInSameGroup(point)) continue;

                if(AdjacentIsSameHeight(point)) return true;
                if(AdjacentEdgeIsSlope(point)) return true;
                
            }
        }
        return false;
    }

    bool AdjacentInSameGroup(WorleyNoise.PointData point)
    {
        float currentCellGroup = topology.CellGrouping(point.currentCellIndex);
        float adjacentCellGroup = topology.CellGrouping(point.adjacentCellIndex);
        return currentCellGroup == adjacentCellGroup;
    }

    bool AdjacentIsSameHeight(WorleyNoise.PointData point)
    {
        float currentCellHeight = topology.CellHeight(point.currentCellIndex);
        float adjacentCellHeight = topology.CellHeight(point.adjacentCellIndex);
        return currentCellHeight == adjacentCellHeight;
    }

    bool AdjacentEdgeIsSlope(WorleyNoise.PointData point)
    {
        int2 adjacentDirection = point.adjacentCellIndex - point.currentCellIndex;
        return topology.EdgeIsSloped(adjacentDirection, point);
    }
}

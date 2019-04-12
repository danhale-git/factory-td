using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

public class SectorSystem : ComponentSystem
{
    EntityManager entityManager;
    CellSystem cellSystem;

    WorleyNoise worley;
    TopologyUtil topologyUtil;

    EntityQuery sectorGroup;

    DynamicBuffer<SectorCell> sectorCells;
    DynamicBuffer<AdjacentCell> adjacentCells;

    public enum SectorTypes { NONE, UNPATHABLE, LAKE }

    public struct TypeComponent : IComponentData
    {
        public SectorTypes Value;
    }

    public struct SectorNoiseValue : IComponentData
    {
        public float Value;
    }
    
    [InternalBufferCapacity(0)]
    public struct CellSet : IBufferElementData
    {
        public WorleyNoise.PointData data;
    }

    [InternalBufferCapacity(0)]
    public struct AdjacentCell : IBufferElementData
    {
        public WorleyNoise.CellData data;
    }

    [InternalBufferCapacity(0)]
    public struct SectorCell : IBufferElementData
    {
        public WorleyNoise.CellData data;
    }

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;
        cellSystem = World.Active.GetOrCreateSystem<CellSystem>();

        worley = TerrainSettings.CellWorley();
        topologyUtil = new TopologyUtil();

        EntityQueryDesc sectorQuery = new EntityQueryDesc{
            All = new ComponentType[] { typeof(Tags.TerrainCell), typeof(CellSet) },
            None = new ComponentType[] { typeof(TypeComponent) }
        };
        sectorGroup = GetEntityQuery(sectorQuery);
    }

    protected override void OnUpdate()
    {
        var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        var chunks = sectorGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        var entityType = GetArchetypeChunkEntityType();
        var startCellType = GetArchetypeChunkComponentType<WorleyNoise.CellData>(true);
        var cellArrayType = GetArchetypeChunkBufferType<CellSet>();

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];
            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<WorleyNoise.CellData> startCells = chunk.GetNativeArray(startCellType);
            BufferAccessor<CellSet> cellArrays = chunk.GetBufferAccessor(cellArrayType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity sectorEntity = entities[e];
                DynamicBuffer<CellSet> cellSet = cellArrays[e];

                sectorCells = commandBuffer.AddBuffer<SectorCell>(sectorEntity);
                adjacentCells = commandBuffer.AddBuffer<AdjacentCell>(sectorEntity);

                float grouping = topologyUtil.CellGrouping(startCells[e].index);
                SortCellData(sectorEntity, grouping, cellSet);
                
                float value = sectorCells[0].data.value;
                commandBuffer.AddComponent<SectorNoiseValue>(sectorEntity, new SectorNoiseValue{ Value = value });

                TypeComponent type = new TypeComponent();
                commandBuffer.AddComponent<TypeComponent>(sectorEntity, type); 
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    void SortCellData(Entity sectorEntity, float grouping, DynamicBuffer<CellSet> set)
    {
        for(int i = 0; i < set.Length; i++)
        {
            WorleyNoise.CellData cellData = worley.GetCellData(set[i].data.currentCellIndex);

            if(cellData.value == 0) continue;

            if(topologyUtil.CellGrouping(set[i].data.currentCellIndex) != grouping)
            {
                adjacentCells.Add(new AdjacentCell{ data = cellData });
            }
            else
            {
                sectorCells.Add(new SectorCell{ data = cellData });
                cellSystem.TrySetCell(sectorEntity, set[i].data.currentCellIndex);
            }
        }
    }

    float GetSectorValue()
    {
        float value = 0;
        for(int i = 0; i < sectorCells.Length; i++)
            value += sectorCells[i].data.value;

        return value / sectorCells.Length;
    }

    /*bool SectorIsLowest(int2 cellIndex)
    {
        return topologyUtil.CellHeight(cellIndex) <= TerrainSettings.cellheightMultiplier; 
    }

    bool SectorIsPathable(DynamicBuffer<SectorCell> cellBuffer)
    {
        for(int i = 0; i < cellBuffer.Length; i++)
        {
            DynamicBuffer<WorleyNoise.PointData> points = entityManager.GetBuffer<WorleyNoise.PointData>(cellBuffer[i].entity);
            for(int p = 0; p < points.Length; p++)
            {
                WorleyNoise.PointData point = points[p];
                if(PointIsOutsideCell(point, cellBuffer[i].data)) continue;

                if(point.isSet == 0) continue;
                if(AdjacentInSameGroup(point)) continue;

                if(AdjacentIsSameHeight(point)) return true;
                if(AdjacentEdgeIsSlope(point)) return true;
                
            }
        }
        return false;
    }

    bool PointIsOutsideCell(WorleyNoise.PointData point, WorleyNoise.CellData cell)
    {
        return (point.isSet == 0) || !point.currentCellIndex.Equals(cell.index);
    }

    bool AdjacentInSameGroup(WorleyNoise.PointData point)
    {
        float currentCellGroup = topologyUtil.CellGrouping(point.currentCellIndex);
        float adjacentCellGroup = topologyUtil.CellGrouping(point.adjacentCellIndex);
        return currentCellGroup == adjacentCellGroup;
    }

    bool AdjacentIsSameHeight(WorleyNoise.PointData point)
    {
        float currentCellHeight = topologyUtil.CellHeight(point.currentCellIndex);
        float adjacentCellHeight = topologyUtil.CellHeight(point.adjacentCellIndex);
        return currentCellHeight == adjacentCellHeight;
    }

    bool AdjacentEdgeIsSlope(WorleyNoise.PointData point)
    {
        int2 adjacentDirection = point.adjacentCellIndex - point.currentCellIndex;
        return topologyUtil.EdgeIsSloped(adjacentDirection, point);
    }  */
}

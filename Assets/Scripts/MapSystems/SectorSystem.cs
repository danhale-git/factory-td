using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace Tags
{
    public struct CreateWaterEntity : IComponentData { }
}

public class SectorSystem : ComponentSystem
{
    EntityManager entityManager;
    CellSystem cellSystem;

    TopologyUtil topologyUtil;

    EntityQuery sectorGroup;

    public enum SectorTypes { NONE, MOUNTAIN, LAKE, GULLY }

    public struct TypeComponent : IComponentData
    {
        public SectorTypes Value;
    }

    public struct MasterCell : IComponentData
    {
        public WorleyNoise.CellData Value;
    }

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;
        cellSystem = World.Active.GetOrCreateSystem<CellSystem>();

        topologyUtil = new TopologyUtil().Construct();

        EntityQueryDesc sectorQuery = new EntityQueryDesc{
            All = new ComponentType[] { typeof(Tags.TerrainEntity), typeof(CellSystem.AdjacentCell), typeof(CellSystem.SectorCell) },
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
        var pointArrayType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);
        var sectorCellType = GetArchetypeChunkBufferType<CellSystem.SectorCell>(true);
        var adjacentCellType = GetArchetypeChunkBufferType<CellSystem.AdjacentCell>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            var chunk = chunks[c];

            var entities = chunk.GetNativeArray(entityType);
            var startCells = chunk.GetNativeArray(startCellType);
            var pointArrays = chunk.GetBufferAccessor(pointArrayType);
            var sectorCellArrays = chunk.GetBufferAccessor(sectorCellType);
            var adjacentCellArrays = chunk.GetBufferAccessor(adjacentCellType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity sectorEntity = entities[e];
                WorleyNoise.CellData startCell = startCells[e];
                DynamicBuffer<WorleyNoise.PointData> points = pointArrays[e];
                DynamicBuffer<CellSystem.SectorCell> sectorCells = sectorCellArrays[e];
                DynamicBuffer<CellSystem.AdjacentCell> adjacentCells = adjacentCellArrays[e];

                WorleyNoise.CellData masterCell = cellSystem.GetCellData(sectorCells[0].index);

                float grouping = topologyUtil.CellGrouping(startCell.index);
                bool pathable = SectorIsPathable(points, grouping);
                int height = (int)topologyUtil.CellHeight(masterCell.index);
                
                TypeComponent type = new TypeComponent();
                if(!pathable)
                {
                    if(AllAdjacentAreHigher(adjacentCells, topologyUtil.CellHeightGroup(masterCell.index)))
                        type.Value = SectorTypes.GULLY;
                    else
                        type.Value = SectorTypes.MOUNTAIN;
                }
                else if(topologyUtil.CellHeightGroup(masterCell.index) < 2 && sectorCells.Length > 2)
                {
                    type.Value = SectorTypes.LAKE;
                    commandBuffer.AddComponent<Tags.CreateWaterEntity>(sectorEntity, new Tags.CreateWaterEntity());
                }

                commandBuffer.AddComponent<TypeComponent>(sectorEntity, type); 
                commandBuffer.AddComponent<MasterCell>(sectorEntity, new MasterCell{ Value = masterCell }); 
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    bool AllAdjacentAreHigher(DynamicBuffer<CellSystem.AdjacentCell> adjacentCells, float heightGrouping)
    {
        for(int i = 0; i < adjacentCells.Length; i++)
        {
            WorleyNoise.CellData cell = cellSystem.GetCellData(adjacentCells[i].index);
            if(topologyUtil.CellHeightGroup(cell.index) <= heightGrouping)
                return false;
        }
        return true;
    }

    bool SectorIsPathable(DynamicBuffer<WorleyNoise.PointData> points, float grouping)
    {
        for(int i = 0; i < points.Length; i++)
        {
            WorleyNoise.PointData point = points[i];
            if(PointIsOutsideGroup(point, grouping)) continue;
            if(AdjacentInSameGroup(point)) continue;

            if(AdjacentIsSameHeight(point)) return true;
            if(AdjacentEdgeIsSlope(point)) return true;
        }
        return false;
    }

    bool PointIsOutsideGroup(WorleyNoise.PointData point, float grouping)
    {
        return !point.isSet || (cellSystem.GetCellGrouping(point.currentCellIndex) != grouping);
    }

    bool AdjacentInSameGroup(WorleyNoise.PointData point)
    {
        float currentCellGroup = cellSystem.GetCellGrouping(point.currentCellIndex);
        float adjacentCellGroup = cellSystem.GetCellGrouping(point.adjacentCellIndex);
        return currentCellGroup == adjacentCellGroup;
    }

    bool AdjacentIsSameHeight(WorleyNoise.PointData point)
    {
        float currentCellHeight = cellSystem.GetCellHeight(point.currentCellIndex);
        float adjacentCellHeight = cellSystem.GetCellHeight(point.adjacentCellIndex);
        return currentCellHeight == adjacentCellHeight;
    }

    bool AdjacentEdgeIsSlope(WorleyNoise.PointData point)
    {
        return topologyUtil.EdgeIsSloped(point);
    }
}

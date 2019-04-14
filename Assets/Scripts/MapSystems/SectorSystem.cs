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

    WorleyNoise worley;
    TopologyUtil topologyUtil;

    EntityQuery sectorGroup;

    public enum SectorTypes { NONE, MOUNTAIN, LAKE, GULLY }

    public struct TypeComponent : IComponentData
    {
        public SectorTypes Value;
    }

    public struct SectorNoiseValue : IComponentData
    {
        public float Value;
    }

    public struct SectorGrouping : IComponentData
    {
        public float Value;
    }

    public struct MasterCell : IComponentData
    {
        public WorleyNoise.CellData Value;
    }

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;
        cellSystem = World.Active.GetOrCreateSystem<CellSystem>();

        worley = TerrainSettings.CellWorley();
        topologyUtil = new TopologyUtil();

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
            ArchetypeChunk chunk = chunks[c];
            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<WorleyNoise.CellData> startCells = chunk.GetNativeArray(startCellType);
            BufferAccessor<WorleyNoise.PointData> pointArrays = chunk.GetBufferAccessor(pointArrayType);
            BufferAccessor<CellSystem.SectorCell> sectorCellArrays = chunk.GetBufferAccessor(sectorCellType);
            BufferAccessor<CellSystem.AdjacentCell> adjacentCellArrays = chunk.GetBufferAccessor(adjacentCellType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity sectorEntity = entities[e];
                DynamicBuffer<WorleyNoise.PointData> points = pointArrays[e];

                float grouping = topologyUtil.CellGrouping(startCells[e].index);
                
                WorleyNoise.CellData masterCell = sectorCellArrays[e][0].data;
                commandBuffer.AddComponent<SectorNoiseValue>(sectorEntity, new SectorNoiseValue{ Value = masterCell.value });

                TypeComponent type = new TypeComponent();

                bool pathable = SectorIsPathable(points, grouping);
                int height = (int)topologyUtil.CellHeight(masterCell.index);
                
                if(!pathable)
                {
                    if(AllAdjacentAreHigher(adjacentCellArrays[e], topologyUtil.CellHeightGroup(masterCell.index)))
                        type.Value = SectorTypes.GULLY;
                    else
                        type.Value = SectorTypes.MOUNTAIN;
                }
                else if(topologyUtil.CellHeightGroup(masterCell.index) < 2 && sectorCellArrays[e].Length > 2)
                {
                    type.Value = SectorTypes.LAKE;
                    commandBuffer.AddComponent<Tags.CreateWaterEntity>(sectorEntity, new Tags.CreateWaterEntity());
                }

                commandBuffer.AddComponent<TypeComponent>(sectorEntity, type); 
                commandBuffer.AddComponent<SectorGrouping>(sectorEntity, new SectorGrouping{ Value = grouping }); 
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
            WorleyNoise.CellData cell = adjacentCells[i].data;
            if(topologyUtil.CellHeightGroup(cell.index) <= heightGrouping)
                return false;
        }
        return true;
    }

    bool SectorIsPathable(DynamicBuffer<WorleyNoise.PointData> points, float grouping)
    {
        for(int p = 0; p < points.Length; p++)
        {
            WorleyNoise.PointData point = points[p];
            if(PointIsOutsideGroup(point, grouping)) continue;
            if(AdjacentInSameGroup(point)) continue;

            if(AdjacentIsSameHeight(point)) return true;
            if(AdjacentEdgeIsSlope(point)) return true;
        }
        return false;
    }

    bool PointIsOutsideGroup(WorleyNoise.PointData point, float grouping)
    {
        return (point.isSet == 0) || (topologyUtil.CellGrouping(point.adjacentCellIndex) != grouping);
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
        return topologyUtil.EdgeIsSloped(point);
    }
}

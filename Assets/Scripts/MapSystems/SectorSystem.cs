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
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        NativeArray<ArchetypeChunk> chunks = sectorGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkBufferType<CellSet> cellArrayType = GetArchetypeChunkBufferType<CellSet>();

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];
            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            BufferAccessor<CellSet> cellArrays = chunk.GetBufferAccessor(cellArrayType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity sectorEntity = entities[e];
                DynamicBuffer<CellSet> cellSet = cellArrays[e];

                WorleyNoise.CellData startCell = entityManager.GetComponentData<WorleyNoise.CellData>(sectorEntity);

                TypeComponent type = new TypeComponent();
                
                float value = entityManager.GetComponentData<WorleyNoise.CellData>(sectorEntity).value;

                commandBuffer.AddComponent<SectorNoiseValue>(sectorEntity, new SectorNoiseValue{ Value = value });
                commandBuffer.AddComponent<TypeComponent>(sectorEntity, type); 

                DynamicBuffer<SectorCell> sectorCells = commandBuffer.AddBuffer<SectorCell>(sectorEntity);
                DynamicBuffer<AdjacentCell> adjacentCells = commandBuffer.AddBuffer<AdjacentCell>(sectorEntity);

                float currentGrouping = topologyUtil.CellGrouping(startCell.index);

                for(int i = 0; i < cellSet.Length; i++)
                {
                    WorleyNoise.CellData cellData = worley.GetCellData(cellSet[i].data.currentCellIndex);
                    if(topologyUtil.CellGrouping(cellSet[i].data.currentCellIndex) != currentGrouping)
                    {
                        adjacentCells.Add(new AdjacentCell{ data = cellData });
                    }
                    else
                    {
                        DebugSystem.Cube(cellSet[i].data.currentCellPosition + new float3(0,20,0), new float4(0, 1, 1, 1));
                        
                        sectorCells.Add(new SectorCell{ data = cellData });
                        cellSystem.TrySetCell(sectorEntity, cellSet[i].data.currentCellIndex);
                    }
                }

                /*if(!AllEntitiesHaveWorley(cells))
                    continue;

                float noiseValue = GetSectorValue(cells); */

                /*if(!SectorIsPathable(cells))
                    type.Value = SectorTypes.UNPATHABLE;
                else if(SectorIsLowest(cells[0].data.index) && noiseValue > 0.5f)
                    type.Value = SectorTypes.LAKE;

                AddSectorComponentsToCells(noiseValue, type, cells, commandBuffer);  */ 

                //commandBuffer.AddComponent(sectorEntity, type);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    /*bool SectorIsLowest(int2 cellIndex)
    {
        return topologyUtil.CellHeight(cellIndex) <= TerrainSettings.cellheightMultiplier; 
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

    void AddSectorComponentsToCells(float value, TypeComponent type, DynamicBuffer<Cell> cellBuffer, EntityCommandBuffer commandBuffer)
    {
        for(int i = 0; i < cellBuffer.Length; i++)
        {
            commandBuffer.AddComponent<SectorNoiseValue>(cellBuffer[i].entity, new SectorNoiseValue{ Value = value });
            commandBuffer.AddComponent<TypeComponent>(cellBuffer[i].entity, type);
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
    } */
}

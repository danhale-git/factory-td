using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;

public class TopologySystem : ComponentSystem
{
    EntityManager entityManager;
    CellSystem cellSystem;

    EntityQuery topologyGroup;

    TopologyUtil topologyUtil;

    public struct Height : IBufferElementData
    {
        public float height;
    }

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;
        cellSystem = World.Active.GetOrCreateSystem<CellSystem>();

        topologyUtil = new TopologyUtil().Construct();

        EntityQueryDesc topologyQuery = new EntityQueryDesc{
            All = new ComponentType[] { typeof(WorleyNoise.PointData), typeof(SectorSystem.MasterCell) },
            None = new ComponentType[] { typeof(Height) }
        };
        topologyGroup = GetEntityQuery(topologyQuery);
    }

    protected override void OnUpdate()
    {
        ScheduleTopologyJobs();        
    }

    void ScheduleTopologyJobs()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);

        NativeArray<ArchetypeChunk> chunks = topologyGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        var entityType = GetArchetypeChunkEntityType();
        var sectorTypeType = GetArchetypeChunkComponentType<SectorSystem.TypeComponent>(true);
        var worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);

        var sectorCellArrayType = GetArchetypeChunkBufferType<CellSystem.SectorCell>(true);
        var sectorAdjacentCellArrayType = GetArchetypeChunkBufferType<CellSystem.AdjacentCell>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<SectorSystem.TypeComponent> sectorTypes = chunk.GetNativeArray(sectorTypeType);
            BufferAccessor<WorleyNoise.PointData> worleyArrays = chunk.GetBufferAccessor(worleyType);

            BufferAccessor<CellSystem.SectorCell> sectorCellArrays = chunk.GetBufferAccessor(sectorCellArrayType);
            BufferAccessor<CellSystem.AdjacentCell> sectorAdjacentCellArrays = chunk.GetBufferAccessor(sectorAdjacentCellArrayType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                SectorSystem.SectorTypes sectorType = sectorTypes[e].Value;
                DynamicBuffer<WorleyNoise.PointData> worley = worleyArrays[e];

                DynamicBuffer<CellSystem.SectorCell> sectorCells = sectorCellArrays[e];
                DynamicBuffer<CellSystem.AdjacentCell> sectorAdjacentCells = sectorAdjacentCellArrays[e];

                DynamicBuffer<Height> topology = commandBuffer.AddBuffer<Height>(entity);
                topology.ResizeUninitialized(worley.Length);

                for(int i = 0; i < topology.Length; i++)
                {
                    if(!worley[i].isSet) continue;

                    float3 position = worley[i].pointWorldPosition;
                    WorleyNoise.PointData point = worley[i];
                    Height pointHeight = new Height();

                    bool sloped = topologyUtil.EdgeIsSloped(point);

                    if(sloped && !(sectorType == SectorSystem.SectorTypes.LAKE))
                        pointHeight.height = SmoothSlope(point);  
                    else if(sectorType == SectorSystem.SectorTypes.LAKE)
                        pointHeight.height = Lake(worley[i], sloped);
                    else
                        pointHeight.height = cellSystem.GetCellHeight(worley[i].currentCellIndex);

                    topology[i] = pointHeight;
                }
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    float Lake(WorleyNoise.PointData point, bool sloped)
    {
        float cellHeight = sloped ? SmoothSlope(point) : cellSystem.GetCellHeight(point.currentCellIndex);
        float lakeDepth = math.clamp(point.distance2Edge - 0.3f, 0, 1) * (TerrainSettings.cellheightMultiplier * 3);
        return cellHeight - lakeDepth;
    }

    float SmoothSlope(WorleyNoise.PointData point)
    {
        float currentHeight = cellSystem.GetCellHeight(point.currentCellIndex);
        float adjacentHeight = cellSystem.GetCellHeight(point.adjacentCellIndex);

        if(currentHeight == adjacentHeight) return currentHeight;

        float halfway = (currentHeight + adjacentHeight) / 2;

        float currentHeightGroup = topologyUtil.CellHeightGroup(point.currentCellIndex);
        float adjacentHeightGroup = topologyUtil.CellHeightGroup(point.adjacentCellIndex);

        float difference = math.max(currentHeightGroup, adjacentHeightGroup) - math.min(currentHeightGroup, adjacentHeightGroup);
        int clampedDifference = (int)math.clamp(difference, 1, TerrainSettings.cellHeightLevelCount);

        float adjustedSlopeLength = TerrainSettings.slopeLength * clampedDifference;
        float interpolator;

        if(point.distance2Edge > adjustedSlopeLength/2)
            interpolator = math.smoothstep(0, adjustedSlopeLength, point.distance2Edge);
        else
            interpolator = math.unlerp(0, adjustedSlopeLength, point.distance2Edge);

        return math.lerp(halfway, currentHeight, math.clamp(interpolator, 0, 1));
    }

    float LargestAdjacentHeight(DynamicBuffer<CellSystem.AdjacentCell> adjacent)
    {
        float largest = 0;
        for(int i = 0; i < adjacent.Length; i++)
        {
            float height = topologyUtil.CellHeight(adjacent[i].index);
            if(height > largest)
                largest = height;
        }
            
        return largest;
    }
}
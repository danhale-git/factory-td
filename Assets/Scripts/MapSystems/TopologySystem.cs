using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;

public class TopologySystem : ComponentSystem
{
    EntityManager entityManager;

    EntityQuery topologyGroup;

    TopologyUtil topologyUtil;

    SimplexNoiseGenerator simplex;

    public struct Height : IBufferElementData
    {
        public float height;
    }

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;

        topologyUtil = new TopologyUtil();
        simplex = new SimplexNoiseGenerator(TerrainSettings.seed, 0.01f);

        EntityQueryDesc topologyQuery = new EntityQueryDesc{
            All = new ComponentType[] { typeof(WorleyNoise.CellData), typeof(WorleyNoise.PointData), typeof(SectorSystem.SectorNoiseValue) },
            None = new ComponentType[] { typeof(Unity.Rendering.RenderMesh), typeof(Height) }
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
                    if(worley[i].isSet == 0) continue;

                    float3 position = worley[i].pointWorldPosition;
                    WorleyNoise.PointData point = worley[i];
                    Height pointHeight = new Height();

                    bool pointIsInSector = topologyUtil.CellGrouping(point.currentCellIndex) == topologyUtil.CellGrouping(sectorCells[0].data.index);

                    if(topologyUtil.EdgeIsSloped(point))
                        pointHeight.height = SmoothSlope(point);  
                    else if(sectorType == SectorSystem.SectorTypes.LAKE)
                        pointHeight.height = Lake(worley[i]);
                    else if(pointIsInSector && sectorType == SectorSystem.SectorTypes.MOUNTAIN)
                        pointHeight.height  = Mountain(worley[i], position);
                    else if(sectorType == SectorSystem.SectorTypes.GULLY)
                        pointHeight.height = Gully(point, position);
                    else
                        pointHeight.height = topologyUtil.CellHeight(worley[i].currentCellIndex);

                    topology[i] = pointHeight;
                }
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    float Gully(WorleyNoise.PointData point, float3 position)
    {
        float cellHeight = topologyUtil.CellHeight(point.currentCellIndex);
        float gullyHeight = point.distance2Edge * TerrainSettings.cellheightMultiplier / 2;

        float result = math.lerp(cellHeight, cellHeight-gullyHeight, point.distance2Edge);

        result -= (simplex.GetSimplex(position.x, position.z, 0.1f) * 10) * point.distance2Edge;

        return result;
    }

    float Mountain(WorleyNoise.PointData point, float3 position)
    {
        float cellHeight = topologyUtil.CellHeight(point.currentCellIndex);
        float mountainHeight = point.distance2Edge * TerrainSettings.cellheightMultiplier;

        float result = math.lerp(cellHeight, cellHeight+mountainHeight, point.distance2Edge);

        result += (simplex.GetSimplex(position.x, position.z, 0.1f) * 5) * point.distance2Edge;

        return result;
    }

    float Lake(WorleyNoise.PointData point)
    {
        float cellHeight = topologyUtil.CellHeight(point.currentCellIndex);
        float lakeDepth = math.clamp(point.distance2Edge - 0.3f, 0, 1) * (TerrainSettings.cellheightMultiplier * 3);
        return cellHeight - lakeDepth;
    }

    float SmoothSlope(WorleyNoise.PointData point)
    {
        float currentHeight = topologyUtil.CellHeight(point.currentCellIndex);
        float adjacentHeight = topologyUtil.CellHeight(point.adjacentCellIndex);

        if(currentHeight == adjacentHeight) return currentHeight;

        float currentHeightGroup = topologyUtil.CellHeightGroup(point.currentCellIndex);
        float adjacentHeightGroup = topologyUtil.CellHeightGroup(point.adjacentCellIndex);


        float difference = math.max(currentHeightGroup, adjacentHeightGroup) - math.min(currentHeightGroup, adjacentHeightGroup);
        int clampedDifference = (int)math.clamp(difference, 1, TerrainSettings.cellHeightLevelCount);

        float halfway = (currentHeight + adjacentHeight) / 2;
        float interpolator = math.unlerp(0, TerrainSettings.slopeLength * clampedDifference, point.distance2Edge);

        return math.lerp(halfway, currentHeight, math.clamp(interpolator, 0, 1));
    }

    float LargestAdjacentHeight(DynamicBuffer<CellSystem.AdjacentCell> adjacent)
    {
        float largest = 0;
        for(int i = 0; i < adjacent.Length; i++)
        {
            float height = topologyUtil.CellHeight(adjacent[i].data.index);
            if(height > largest)
                largest = height;
        }
            
        return largest;
    }
}
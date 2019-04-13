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

    public struct Height : IBufferElementData
    {
        public float height;
    }

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;

        topologyUtil = new TopologyUtil();

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

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<SectorSystem.TypeComponent> sectorTypes = chunk.GetNativeArray(sectorTypeType);
            BufferAccessor<WorleyNoise.PointData> worleyArrays = chunk.GetBufferAccessor(worleyType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                SectorSystem.SectorTypes sectorType = sectorTypes[e].Value;
                DynamicBuffer<WorleyNoise.PointData> worley = worleyArrays[e];

                DynamicBuffer<Height> topology = commandBuffer.AddBuffer<Height>(entity);
                topology.ResizeUninitialized(worley.Length);

                for(int i = 0; i < topology.Length; i++)
                {
                    if(worley[i].isSet == 0) continue;

                    float3 position = worley[i].pointWorldPosition;
                    WorleyNoise.PointData point = worley[i];
                    Height pointHeight = new Height();

                    if(topologyUtil.EdgeIsSloped(point))
                        pointHeight.height = SmoothSlope(point);  
                    else
                        pointHeight.height = topologyUtil.CellHeight(worley[i].currentCellIndex);

                    if(sectorType == SectorSystem.SectorTypes.MOUNTAIN)
                    {
                        pointHeight.height += (point.distance2Edge - 0.3f) * (TerrainSettings.cellheightMultiplier * 3);
                    }

                    topology[i] = pointHeight;
                }
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
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
}
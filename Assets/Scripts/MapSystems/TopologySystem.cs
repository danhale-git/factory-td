using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;

public class TopologySystem : ComponentSystem
{
    EntityManager entityManager;

    ComponentGroup topologyGroup;

    TopologyUtil topologyUtil;

    public struct Height : IBufferElementData
    {
        public float height;
    }

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        topologyUtil = new TopologyUtil();

        EntityArchetypeQuery topologyQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(WorleyNoise.CellData), typeof(WorleyNoise.PointData), typeof(SectorSystem.SectorNoiseValue) },
            None = new ComponentType[] { typeof(CellSystem.CellComplete), typeof(Height) }
        };
        topologyGroup = GetComponentGroup(topologyQuery);
    }

    protected override void OnUpdate()
    {
        ScheduleTopologyJobs();        
    }

    void ScheduleTopologyJobs()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);

        NativeArray<ArchetypeChunk> chunks = topologyGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<SectorSystem.TypeComponent> sectorTypeType = GetArchetypeChunkComponentType<SectorSystem.TypeComponent>(true);
        ArchetypeChunkBufferType<WorleyNoise.PointData> worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<SectorSystem.TypeComponent> sectorTypes = chunk.GetNativeArray(sectorTypeType);
            BufferAccessor<WorleyNoise.PointData> worleyArrays = chunk.GetBufferAccessor(worleyType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                SectorSystem.SectorTypes type = sectorTypes[e].Value;
                DynamicBuffer<WorleyNoise.PointData> worley = worleyArrays[e];

                DynamicBuffer<Height> topology = commandBuffer.AddBuffer<Height>(entity);
                topology.ResizeUninitialized(worley.Length);

                for(int i = 0; i < topology.Length; i++)
                {
                    if(worley[i].isSet == 0) continue;

                    float3 position = worley[i].pointWorldPosition;
                    WorleyNoise.PointData point = worley[i];
                    Height pointHeight = new Height();

                    int2 adjacentDirection = point.adjacentCellIndex - point.currentCellIndex;

                    if(topologyUtil.EdgeIsSloped(adjacentDirection, point))
                        pointHeight.height = SmoothSlope(point);  
                    else
                        pointHeight.height = topologyUtil.CellHeight(worley[i].currentCellIndex);

                    if(type == SectorSystem.SectorTypes.LAKE && point.distance2Edge > 0.3f)
                        pointHeight.height -= (point.distance2Edge - 0.3f) * (TerrainSettings.cellheightMultiplier * 3);

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

        float halfway = (currentHeight + adjacentHeight) / 2;
        float interpolator = math.unlerp(0, 0.35f, point.distance2Edge);

        return math.lerp(halfway, currentHeight, math.clamp(interpolator, 0, 1));
    }
}
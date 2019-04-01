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

    SimplexNoiseGenerator simplex;
    
    Biomes biomes;

    public struct Height : IBufferElementData
    {
        public float height;
    }

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        simplex = new SimplexNoiseGenerator(TerrainSettings.seed, 0.1f);

        biomes = new Biomes();

        EntityArchetypeQuery topologyQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(WorleyNoise.CellData), typeof(WorleyNoise.PointData) },
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
        ArchetypeChunkBufferType<WorleyNoise.PointData> worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            BufferAccessor<WorleyNoise.PointData> worleyBuffers = chunk.GetBufferAccessor(worleyType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                DynamicBuffer<WorleyNoise.PointData> worley = worleyBuffers[e];

                DynamicBuffer<Height> topologyBuffer = commandBuffer.AddBuffer<Height>(entity);
                topologyBuffer.ResizeUninitialized(worley.Length);


                for(int i = 0; i < topologyBuffer.Length; i++)
                {
                    if(worley[i].isSet == 0) continue;

                    float3 position = worley[i].pointWorldPosition;

                    //float height = worley[i].currentCellValue * (2*biomes.CellGrouping(worley[i].currentCellValue)) + noise;

                    //float height = biomes.GetIndex(worley[i].currentCellValue) * 5;

                    WorleyNoise.PointData point = worley[i];

                    int2 slopeDirection = biomes.SlopedSide(point);

                    
                    float height;

                    SimplexNoiseGenerator biomeSimplex = new SimplexNoiseGenerator(TerrainSettings.seed, 0.2f);
                    if((point.adjacentCellIndex - point.currentCellIndex).Equals(slopeDirection))
                    {

                        float currentHeight = biomes.CellHeight(worley[i].currentCellValue);
                        float adjacentHeight = biomes.CellHeight(worley[i].adjacentCellValue);

                        float halfway = (currentHeight + adjacentHeight) / 2;
                        float interpolator = math.unlerp(0, 1f, point.distance2Edge);

                        DebugSystem.Text("Distance to edge", point.distance2Edge.ToString());

                        height = math.lerp(halfway, currentHeight, point.distance2Edge);    
                    }
                    else
                    {
                        height = biomes.CellHeight(worley[i].currentCellValue);
                    }

                    topologyBuffer[i] = new Height{ height = height };
                }
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }
}
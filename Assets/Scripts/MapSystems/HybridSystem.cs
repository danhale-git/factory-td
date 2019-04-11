using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Rendering;

public class HybridSystem : ComponentSystem
{
    EntityQuery hybridQuery;

    protected override void OnCreate()
    {
        hybridQuery = GetEntityQuery(
            new EntityQueryDesc{
                All = new ComponentType[] { typeof(RenderMesh), typeof(Tags.TerrainCell) }
            }
        );
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);

        NativeArray<ArchetypeChunk> chunks = hybridQuery.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
            }
        }
    }
}

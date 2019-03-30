using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;

public class MeshDataSystem : ComponentSystem
{
    EntityManager entityManager;

    ComponentGroup meshDataGroup;

    [InternalBufferCapacity(0)]
	public struct MeshVertex : IBufferElementData
	{
		public float3 vertex;
	}

	[InternalBufferCapacity(0)]
	public struct MeshNormal : IBufferElementData
	{
		public float3 normal;
	}

    [InternalBufferCapacity(0)]
	public struct MeshVertColor : IBufferElementData
	{
		public float4 color;
	}
    
	[InternalBufferCapacity(0)]
	public struct MeshTriangle : IBufferElementData
	{
		public int triangle;
	}
	

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        EntityArchetypeQuery meshDataQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(WorleyNoise.CellData) },
            None = new ComponentType[] { typeof(DiscoverCell.CellComplete) }
        };
        meshDataGroup = GetComponentGroup(meshDataQuery);
    }

    protected override void OnUpdate()
    {
        
    }

    void ScheduleTopologyJobs()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);

        NativeArray<ArchetypeChunk> chunks = meshDataGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkBufferType<WorleyNoise.PointData> worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);
        ArchetypeChunkBufferType<TopologySystem.Topology> topologyType = GetArchetypeChunkBufferType<TopologySystem.Topology>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            BufferAccessor<WorleyNoise.PointData> worleyBuffers = chunk.GetBufferAccessor(worleyType);
            BufferAccessor<TopologySystem.Topology> TopologyBuffers = chunk.GetBufferAccessor(topologyType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                DynamicBuffer<WorleyNoise.PointData> worley = worleyBuffers[e];
                DynamicBuffer<TopologySystem.Topology> topology = TopologyBuffers[e];

            }
        }
    }
}
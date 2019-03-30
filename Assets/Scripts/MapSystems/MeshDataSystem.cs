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
            All = new ComponentType[] { typeof(WorleyNoise.CellData), typeof(TopologySystem.Topology) },
            None = new ComponentType[] { typeof(DiscoverCell.CellComplete), typeof(MeshVertex) }
        };
        meshDataGroup = GetComponentGroup(meshDataQuery);
    }

    protected override void OnUpdate()
    {
        ScheduleMeshDataJobs();
    }

    void ScheduleMeshDataJobs()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);

        NativeArray<ArchetypeChunk> chunks = meshDataGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkComponentType<DiscoverCell.CellMatrix> matrixType = GetArchetypeChunkComponentType<DiscoverCell.CellMatrix>(true);
        ArchetypeChunkComponentType<WorleyNoise.CellData> cellType = GetArchetypeChunkComponentType<WorleyNoise.CellData>(true);

        ArchetypeChunkBufferType<WorleyNoise.PointData> worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);
        ArchetypeChunkBufferType<TopologySystem.Topology> topologyType = GetArchetypeChunkBufferType<TopologySystem.Topology>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<DiscoverCell.CellMatrix> matrices = chunk.GetNativeArray(matrixType);
            NativeArray<WorleyNoise.CellData> cells = chunk.GetNativeArray(cellType);

            BufferAccessor<WorleyNoise.PointData> worleyBuffers = chunk.GetBufferAccessor(worleyType);
            BufferAccessor<TopologySystem.Topology> TopologyBuffers = chunk.GetBufferAccessor(topologyType);

            for(int e = 0; e < entities.Length; e++)
            {
                ArrayUtil arrayUtil = new ArrayUtil();

                Entity entity = entities[e];
                DiscoverCell.CellMatrix matrix = matrices[e];
                WorleyNoise.CellData cell = cells[e];

                DynamicBuffer<WorleyNoise.PointData> worley = worleyBuffers[e];
                DynamicBuffer<TopologySystem.Topology> topology = TopologyBuffers[e];

                DynamicBuffer<MeshVertex> vertices = commandBuffer.AddBuffer<MeshVertex>(entity);
                DynamicBuffer<MeshTriangle> triangles = commandBuffer.AddBuffer<MeshTriangle>(entity);

                GameObject test1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                test1.transform.Translate(matrix.root);

                GameObject test2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                test2.transform.Translate(matrix.root + matrix.width);


                int indexOffset = 0;

                for(int x = 0; x < matrix.width-1; x++)
                    for(int z = 0; z < matrix.width-1; z++)
                    {

                        int2 bl = new int2(x,   z  );
                        int2 tl = new int2(x,   z+1);
                        int2 tr = new int2(x+1, z+1);
                        int2 br = new int2(x+1, z  );

                        TopologySystem.Topology bottomLeft    = matrix.GetItem<TopologySystem.Topology>(bl, topology, arrayUtil);
                        TopologySystem.Topology topLeft       = matrix.GetItem<TopologySystem.Topology>(tl, topology, arrayUtil);
                        TopologySystem.Topology topRight      = matrix.GetItem<TopologySystem.Topology>(tr, topology, arrayUtil);
                        TopologySystem.Topology bottomRight   = matrix.GetItem<TopologySystem.Topology>(br, topology, arrayUtil);

                        float3 bottomLeftOffset =   new float3(bl.x, bottomLeft.height, bl.y);
                        float3 topLeftOffset =      new float3(tl.x, bottomLeft.height, tl.y);
                        float3 topRightOffset =     new float3(tr.x, bottomLeft.height, tr.y);
                        float3 bottomRightOffset =  new float3(br.x, bottomLeft.height, br.y);

                        vertices.Add(new MeshVertex{ vertex = bottomLeftOffset });
                        vertices.Add(new MeshVertex{ vertex = topLeftOffset });
                        vertices.Add(new MeshVertex{ vertex = topRightOffset });
                        vertices.Add(new MeshVertex{ vertex = bottomRightOffset });

                        triangles.Add(new MeshTriangle{ triangle = 0 + indexOffset });
                        triangles.Add(new MeshTriangle{ triangle = 1 + indexOffset });
                        triangles.Add(new MeshTriangle{ triangle = 2 + indexOffset });
                        triangles.Add(new MeshTriangle{ triangle = 0 + indexOffset });
                        triangles.Add(new MeshTriangle{ triangle = 2 + indexOffset });
                        triangles.Add(new MeshTriangle{ triangle = 3 + indexOffset });

                        indexOffset += 4;
                    }

                Debug.Log("vertices.Length: "+vertices.Length);
                Debug.Log("triangles.Length: "+triangles.Length);
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }
}
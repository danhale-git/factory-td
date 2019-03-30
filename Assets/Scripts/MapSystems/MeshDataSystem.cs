using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;

using ECSMesh;

public class MeshDataSystem : ComponentSystem
{
    EntityManager entityManager;

    ComponentGroup meshDataGroup;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        EntityArchetypeQuery meshDataQuery = new EntityArchetypeQuery{
            All = new ComponentType[] { typeof(WorleyNoise.CellData), typeof(TopologySystem.Topology) },
            None = new ComponentType[] { typeof(CellSystem.CellComplete), typeof(Vertex) }
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
        ArchetypeChunkComponentType<CellSystem.CellMatrix> matrixType = GetArchetypeChunkComponentType<CellSystem.CellMatrix>(true);
        ArchetypeChunkComponentType<WorleyNoise.CellData> cellType = GetArchetypeChunkComponentType<WorleyNoise.CellData>(true);

        ArchetypeChunkBufferType<WorleyNoise.PointData> worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);
        ArchetypeChunkBufferType<TopologySystem.Topology> topologyType = GetArchetypeChunkBufferType<TopologySystem.Topology>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<CellSystem.CellMatrix> matrices = chunk.GetNativeArray(matrixType);
            NativeArray<WorleyNoise.CellData> cells = chunk.GetNativeArray(cellType);

            BufferAccessor<WorleyNoise.PointData> worleyBuffers = chunk.GetBufferAccessor(worleyType);
            BufferAccessor<TopologySystem.Topology> TopologyBuffers = chunk.GetBufferAccessor(topologyType);

            for(int e = 0; e < entities.Length; e++)
            {
                ArrayUtil arrayUtil = new ArrayUtil();

                Entity entity = entities[e];
                CellSystem.CellMatrix matrix = matrices[e];
                WorleyNoise.CellData cell = cells[e];

                DynamicBuffer<WorleyNoise.PointData> worley = worleyBuffers[e];
                DynamicBuffer<TopologySystem.Topology> topology = TopologyBuffers[e];

                DynamicBuffer<Vertex> vertices = commandBuffer.AddBuffer<Vertex>(entity);
                DynamicBuffer<VertColor> colors = commandBuffer.AddBuffer<VertColor>(entity);
                DynamicBuffer<Triangle> triangles = commandBuffer.AddBuffer<Triangle>(entity);

                int indexOffset = 0;

                for(int x = 0; x < matrix.width-1; x++)
                    for(int z = 0; z < matrix.width-1; z++)
                    {

                        int2 bl = new int2(x,   z  );
                        int2 tl = new int2(x,   z+1);
                        int2 tr = new int2(x+1, z+1);
                        int2 br = new int2(x+1, z  );

                        if( matrix.GetItem<WorleyNoise.PointData>(bl, worley, arrayUtil).isSet == 0 ||
                            matrix.GetItem<WorleyNoise.PointData>(tl, worley, arrayUtil).isSet == 0 ||
                            matrix.GetItem<WorleyNoise.PointData>(tr, worley, arrayUtil).isSet == 0 ||
                            matrix.GetItem<WorleyNoise.PointData>(br, worley, arrayUtil).isSet == 0
                        )
                        {
                            continue;
                        }

                        TopologySystem.Topology bottomLeft    = matrix.GetItem<TopologySystem.Topology>(bl, topology, arrayUtil);
                        TopologySystem.Topology topLeft       = matrix.GetItem<TopologySystem.Topology>(tl, topology, arrayUtil);
                        TopologySystem.Topology topRight      = matrix.GetItem<TopologySystem.Topology>(tr, topology, arrayUtil);
                        TopologySystem.Topology bottomRight   = matrix.GetItem<TopologySystem.Topology>(br, topology, arrayUtil);

                        float3 bottomLeftOffset =   new float3(bl.x, bottomLeft.height,     bl.y);
                        float3 topLeftOffset =      new float3(tl.x, topLeft.height,        tl.y);
                        float3 topRightOffset =     new float3(tr.x, topRight.height,       tr.y);
                        float3 bottomRightOffset =  new float3(br.x, bottomRight.height,    br.y);

                        vertices.Add(new Vertex{ vertex = bottomLeftOffset });
                        vertices.Add(new Vertex{ vertex = topLeftOffset });
                        vertices.Add(new Vertex{ vertex = topRightOffset });
                        vertices.Add(new Vertex{ vertex = bottomRightOffset });

                        triangles.Add(new Triangle{ triangle = 0 + indexOffset });
                        triangles.Add(new Triangle{ triangle = 1 + indexOffset });
                        triangles.Add(new Triangle{ triangle = 2 + indexOffset });
                        triangles.Add(new Triangle{ triangle = 0 + indexOffset });
                        triangles.Add(new Triangle{ triangle = 2 + indexOffset });
                        triangles.Add(new Triangle{ triangle = 3 + indexOffset });

                        colors.Add(new VertColor{ color = new float4(0.7f, 0.7f, 0.7f, 1) });
                        colors.Add(new VertColor{ color = new float4(0.7f, 0.7f, 0.7f, 1) });
                        colors.Add(new VertColor{ color = new float4(0.7f, 0.7f, 0.7f, 1) });
                        colors.Add(new VertColor{ color = new float4(0.7f, 0.7f, 0.7f, 1) });

                        indexOffset += 4;
                    }
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }
}
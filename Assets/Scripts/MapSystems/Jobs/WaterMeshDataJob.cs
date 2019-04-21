using Unity.Jobs;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Burst;

using ECSMesh;

namespace MapGeneration
{
    public struct WaterMeshDataJob : IJob
    {
        public EntityCommandBuffer commandBuffer;

        DynamicBuffer<Vertex> vertices;
        DynamicBuffer<VertColor> colors;
        DynamicBuffer<Triangle> triangles;

        [ReadOnly] public EntityArchetype waterEntityArchetype;
        [ReadOnly] public CellSystem.MatrixComponent matrix;
        [ReadOnly] public WorleyNoise.CellData masterCell;

        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<WorleyNoise.PointData> worley;

        [ReadOnly] public TopologyUtil topologyUtil;
        [ReadOnly] public ArrayUtil arrayUtil;

        public void Execute()
        {
            Entity waterEntity = commandBuffer.CreateEntity(waterEntityArchetype);
            commandBuffer.SetComponent<Translation>(waterEntity, new Translation { Value = new float3(matrix.root.x, 0, matrix.root.z) });

            vertices = commandBuffer.AddBuffer<Vertex>(waterEntity);
            colors = commandBuffer.AddBuffer<VertColor>(waterEntity);
            triangles = commandBuffer.AddBuffer<Triangle>(waterEntity);

            int indexOffset = 0;

            for(int x = 0; x < matrix.width-1; x++)
                for(int z = 0; z < matrix.width-1; z++)
                {
                    int2 bl = new int2(x,   z  );
                    int2 tl = new int2(x,   z+1);
                    int2 tr = new int2(x+1, z+1);
                    int2 br = new int2(x+1, z  );

                    WorleyNoise.PointData blWorley = matrix.GetItem<WorleyNoise.PointData>(bl, worley, arrayUtil);
                    WorleyNoise.PointData tlWorley = matrix.GetItem<WorleyNoise.PointData>(tl, worley, arrayUtil);
                    WorleyNoise.PointData trWorley = matrix.GetItem<WorleyNoise.PointData>(tr, worley, arrayUtil);
                    WorleyNoise.PointData brWorley = matrix.GetItem<WorleyNoise.PointData>(br, worley, arrayUtil);

                    if( !blWorley.isSet ||
                        !tlWorley.isSet ||
                        !trWorley.isSet ||
                        !brWorley.isSet )
                    {
                        continue;
                    }

                    float waterHeight = topologyUtil.CellHeight(masterCell.index);
                    float4 waterColor = new float4(0, 0.5f, 1, 0.5f);

                    vertices.Add(new Vertex{ vertex = new float3(bl.x, waterHeight-1, bl.y) });
                    vertices.Add(new Vertex{ vertex = new float3(tl.x, waterHeight-1, tl.y) });
                    vertices.Add(new Vertex{ vertex = new float3(tr.x, waterHeight-1, tr.y) });
                    vertices.Add(new Vertex{ vertex = new float3(br.x, waterHeight-1, br.y) });

                    colors.Add(new VertColor{ color = waterColor });
                    colors.Add(new VertColor{ color = waterColor });
                    colors.Add(new VertColor{ color = waterColor });
                    colors.Add(new VertColor{ color = waterColor });

                    GetTriangleIndicesForQuad(indexOffset);

                    indexOffset += 4;
                }
        }
 
        void GetTriangleIndicesForQuad(int indexOffset)
        {
            triangles.Add(new Triangle{ triangle = 0 + indexOffset });
            triangles.Add(new Triangle{ triangle = 1 + indexOffset });
            triangles.Add(new Triangle{ triangle = 2 + indexOffset });
            triangles.Add(new Triangle{ triangle = 0 + indexOffset });
            triangles.Add(new Triangle{ triangle = 2 + indexOffset });
            triangles.Add(new Triangle{ triangle = 3 + indexOffset });
        }


    }
}
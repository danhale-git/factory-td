using Unity.Jobs;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Burst;

using ECSMesh;

namespace MapGeneration
{
    public struct TerrainMeshDataJob : IJob
    {
        public EntityCommandBuffer commandBuffer;

        DynamicBuffer<Vertex> vertices;
        DynamicBuffer<VertColor> colors;
        DynamicBuffer<Triangle> triangles;

        [ReadOnly] public Entity sectorEntity;
        [ReadOnly] public SectorSystem.SectorTypes sectorType;
        [ReadOnly] public CellSystem.MatrixComponent matrix;

        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<WorleyNoise.PointData> worley;
        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<TopologySystem.Height> pointHeight;

        [ReadOnly] public ArrayUtil arrayUtil;
        [ReadOnly] public TopologyUtil topologyUti;

        public void Execute()
        {
            vertices = commandBuffer.AddBuffer<Vertex>(sectorEntity);
            colors = commandBuffer.AddBuffer<VertColor>(sectorEntity);
            triangles = commandBuffer.AddBuffer<Triangle>(sectorEntity);

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
                        matrix.GetItem<WorleyNoise.PointData>(br, worley, arrayUtil).isSet == 0 )
                    {
                        continue;
                    }

                    TopologySystem.Height blHeight = matrix.GetItem<TopologySystem.Height>(bl, pointHeight, arrayUtil);
                    TopologySystem.Height tlHeight = matrix.GetItem<TopologySystem.Height>(tl, pointHeight, arrayUtil);
                    TopologySystem.Height trHeight = matrix.GetItem<TopologySystem.Height>(tr, pointHeight, arrayUtil);
                    TopologySystem.Height brHeight = matrix.GetItem<TopologySystem.Height>(br, pointHeight, arrayUtil);

                    vertices.Add(new Vertex{ vertex = new float3(bl.x, blHeight.height, bl.y) });
                    vertices.Add(new Vertex{ vertex = new float3(tl.x, tlHeight.height, tl.y) });
                    vertices.Add(new Vertex{ vertex = new float3(tr.x, trHeight.height, tr.y) });
                    vertices.Add(new Vertex{ vertex = new float3(br.x, brHeight.height, br.y) });

                    float difference = LargestHeightDifference(blHeight.height, tlHeight.height, trHeight.height, brHeight.height);
                    WorleyNoise.PointData worleyPoint = matrix.GetItem<WorleyNoise.PointData>(bl, worley, arrayUtil);
                    float4 color = PointColor(worleyPoint, difference);

                    

                    colors.Add(new VertColor{ color = color });
                    colors.Add(new VertColor{ color = color });
                    colors.Add(new VertColor{ color = color });
                    colors.Add(new VertColor{ color = color });

                    GetTriangleDataForPoint(indexOffset);

                    indexOffset += 4;
                }
        }

        float4 PointColor(WorleyNoise.PointData worleyPoint, float difference)
        {
            float4 grey = new float4(0.6f, 0.6f, 0.6f, 1);
            float4 green = new float4(0.2f, 0.6f, 0.1f, 1);

            if(sectorType == SectorSystem.SectorTypes.MOUNTAIN || difference > 1)
            {
                return grey;
            }
            else
            {
                return green;
            }
        }

        float LargestHeightDifference(float a, float b, float c, float d)
        {
            float largest = math.max(a, math.max(b, math.max(c, d)));
            float smallest = math.min(a, math.min(b, math.min(c, d)));
            return largest - smallest;
        }

        void GetTriangleDataForPoint(int indexOffset)
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
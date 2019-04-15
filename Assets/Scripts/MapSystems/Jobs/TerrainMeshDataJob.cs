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
        [ReadOnly] public WorleyNoise.CellData masterCell;

        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<WorleyNoise.PointData> worley;
        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<TopologySystem.Height> pointHeight;

        [ReadOnly] public ArrayUtil arrayUtil;
        [ReadOnly] public TopologyUtil topologyUtil;

        int indexOffset;

        public void Execute()
        {
            vertices = commandBuffer.AddBuffer<Vertex>(sectorEntity);
            colors = commandBuffer.AddBuffer<VertColor>(sectorEntity);
            triangles = commandBuffer.AddBuffer<Triangle>(sectorEntity);

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

                    TopologySystem.Height blHeight = matrix.GetItem<TopologySystem.Height>(bl, pointHeight, arrayUtil);
                    TopologySystem.Height tlHeight = matrix.GetItem<TopologySystem.Height>(tl, pointHeight, arrayUtil);
                    TopologySystem.Height trHeight = matrix.GetItem<TopologySystem.Height>(tr, pointHeight, arrayUtil);
                    TopologySystem.Height brHeight = matrix.GetItem<TopologySystem.Height>(br, pointHeight, arrayUtil);

                    bool northWestToSouthEast = brHeight.height != tlHeight.height;

                    int trianglesAdded = 0;

                    if(northWestToSouthEast)
                    {
                        trianglesAdded += AddVerticesForTriangle(bl, tl, tr, blWorley, tlWorley, trWorley, blHeight.height, tlHeight.height, trHeight.height);
                        trianglesAdded += AddVerticesForTriangle(bl, tr, br, blWorley, trWorley, brWorley, blHeight.height, trHeight.height, brHeight.height);
                    }
                    else
                    {
                        trianglesAdded += AddVerticesForTriangle(bl, tl, br, blWorley, tlWorley, brWorley, blHeight.height, tlHeight.height, brHeight.height);
                        trianglesAdded += AddVerticesForTriangle(tl, tr, br, tlWorley, trWorley, brWorley, tlHeight.height, trHeight.height, brHeight.height);
                    } 

                    for(int i = 0; i < trianglesAdded; i++)
                    {
                        GetIndicesForTriangle();
                        indexOffset += 3;
                    }
                }
        }

        int AddVerticesForTriangle(int2 a, int2 b, int2 c, WorleyNoise.PointData aWorley, WorleyNoise.PointData bWorley, WorleyNoise.PointData cWorley, float aHeight, float bHeight, float cHeight)
        {
            if( aWorley.isSet == 0 ||
                bWorley.isSet == 0 ||
                cWorley.isSet == 0 )
                return 0;

            float masterGrouping = topologyUtil.CellGrouping(masterCell.index);
            float ownerGrouping = topologyUtil.CellGrouping(Owner(aWorley, bWorley, cWorley));

            if(masterGrouping != ownerGrouping) return 0;


            vertices.Add(new Vertex{ vertex = new float3(a.x, aHeight, a.y) });
            vertices.Add(new Vertex{ vertex = new float3(b.x, bHeight, b.y) });
            vertices.Add(new Vertex{ vertex = new float3(c.x, cHeight, c.y) });

            float difference = LargestHeightDifference(aHeight, bHeight, cHeight);
            colors.Add(new VertColor{ color = PointColor(difference) });
            colors.Add(new VertColor{ color = PointColor(difference) });
            colors.Add(new VertColor{ color = PointColor(difference) });

            return 1;
        }

        int2 Owner(WorleyNoise.PointData aWorley, WorleyNoise.PointData bWorley, WorleyNoise.PointData cWorley)
        {
            NativeArray<WorleyNoise.PointData> sortPoints = new NativeArray<WorleyNoise.PointData>(3, Allocator.Temp);
            sortPoints[0] = aWorley;
            sortPoints[1] = bWorley;
            sortPoints[2] = cWorley;

            sortPoints.Sort();
            return sortPoints[0].currentCellIndex;
        }


        float LargestHeightDifference(float a, float b, float c)
        {
            float largest = math.max(a, math.max(b, c));
            float smallest = math.min(a, math.min(b, c));
            return largest - smallest;
        }

        float4 PointColor(float difference)
        {
            float4 grey = new float4(0.6f, 0.6f, 0.6f, 1);
            float4 green = new float4(0.2f, 0.6f, 0.1f, 1);

            if(sectorType == SectorSystem.SectorTypes.GULLY || sectorType == SectorSystem.SectorTypes.MOUNTAIN || difference > 1)
            {
                return grey;
            }
            else
            {
                return green;
            }
        }

        void GetIndicesForTriangle()
        {
            triangles.Add(new Triangle{ triangle = 0 + indexOffset });
            triangles.Add(new Triangle{ triangle = 1 + indexOffset });
            triangles.Add(new Triangle{ triangle = 2 + indexOffset });
        }


    }
}
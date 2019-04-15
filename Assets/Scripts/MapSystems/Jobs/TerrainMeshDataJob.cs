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

                    Vertex blVertex = GetVertex(bl);
                    Vertex tlVertex = GetVertex(tl);
                    Vertex trVertex = GetVertex(tr);
                    Vertex brVertex = GetVertex(br);

                    bool northWestToSouthEast = brVertex.vertex.y != tlVertex.vertex.y;

                    //bool sloped = ( topologyUtil.EdgeIsSloped(blWorley) || topologyUtil.EdgeIsSloped(tlWorley) || topologyUtil.EdgeIsSloped(trWorley) || topologyUtil.EdgeIsSloped(brWorley) )

                    int trianglesAdded = 0;

                    
                    if(northWestToSouthEast)
                    {
                        trianglesAdded += AddVerticesForTriangle(blVertex, tlVertex, trVertex, blWorley, tlWorley, trWorley);
                        trianglesAdded += AddVerticesForTriangle(blVertex, trVertex, brVertex, blWorley, trWorley, brWorley);
                    }
                    else
                    {
                        trianglesAdded += AddVerticesForTriangle(blVertex, tlVertex, brVertex, blWorley, tlWorley, brWorley);
                        trianglesAdded += AddVerticesForTriangle(tlVertex, trVertex, brVertex, tlWorley, trWorley, brWorley);
                    } 

                    for(int i = 0; i < trianglesAdded; i++)
                    {
                        GetIndicesForTriangle();
                        indexOffset += 3;
                    }
                }
        }

        Vertex GetVertex(int2 matrixPosition)
        {
            TopologySystem.Height terrainHeight = matrix.GetItem<TopologySystem.Height>(matrixPosition, pointHeight, arrayUtil);
            return new Vertex() { vertex = new float3(matrixPosition.x, terrainHeight.height, matrixPosition.y) };
        }

        int AddVerticesForTriangle(Vertex a, Vertex b, Vertex c, WorleyNoise.PointData aWorley, WorleyNoise.PointData bWorley, WorleyNoise.PointData cWorley)
        {
            if( aWorley.isSet == 0 ||
                bWorley.isSet == 0 ||
                cWorley.isSet == 0 )
                return 0;

            float masterGrouping = topologyUtil.CellGrouping(masterCell.index);
            float ownerGrouping = topologyUtil.CellGrouping(Owner(aWorley, bWorley, cWorley));

            if(masterGrouping != ownerGrouping) return 0;

            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);

            float difference = LargestHeightDifference(a.vertex.y, b.vertex.y, c.vertex.y);
            colors.Add(new VertColor{ color = PointColor(difference) });
            colors.Add(new VertColor{ color = PointColor(difference) });
            colors.Add(new VertColor{ color = PointColor(difference) });

            return 1;
        }

        int AddVerticesForQuad(Vertex a, Vertex b, Vertex c, Vertex d, WorleyNoise.PointData aWorley, WorleyNoise.PointData bWorley, WorleyNoise.PointData cWorley, WorleyNoise.PointData dWorley)
        {
            if( aWorley.isSet == 0 ||
                bWorley.isSet == 0 ||
                cWorley.isSet == 0 ||
                dWorley.isSet == 0 )
                return 0;

            float masterGrouping = topologyUtil.CellGrouping(masterCell.index);
            float ownerGrouping = topologyUtil.CellGrouping(Owner(aWorley, bWorley, cWorley, dWorley));

            if(masterGrouping != ownerGrouping) return 0;

            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            float difference = LargestHeightDifference(a.vertex.y, b.vertex.y, c.vertex.y, d.vertex.y);
            colors.Add(new VertColor{ color = PointColor(difference) });
            colors.Add(new VertColor{ color = PointColor(difference) });
            colors.Add(new VertColor{ color = PointColor(difference) });
            colors.Add(new VertColor{ color = PointColor(difference) });

            return 2;
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
        int2 Owner(WorleyNoise.PointData aWorley, WorleyNoise.PointData bWorley, WorleyNoise.PointData cWorley, WorleyNoise.PointData dWorley)
        {
            NativeArray<WorleyNoise.PointData> sortPoints = new NativeArray<WorleyNoise.PointData>(3, Allocator.Temp);
            sortPoints[0] = aWorley;
            sortPoints[1] = bWorley;
            sortPoints[2] = cWorley;
            sortPoints[3] = dWorley;

            sortPoints.Sort();
            return sortPoints[0].currentCellIndex;
        }


        float LargestHeightDifference(float a, float b, float c)
        {
            float largest = math.max(a, math.max(b, c));
            float smallest = math.min(a, math.min(b, c));
            return largest - smallest;
        }
        float LargestHeightDifference(float a, float b, float c, float d)
        {
            float largest = math.max(a, math.max(b, math.max(c, d)));
            float smallest = math.min(a, math.min(b, math.min(c, d)));
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
        void GetIndicesForQuad()
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
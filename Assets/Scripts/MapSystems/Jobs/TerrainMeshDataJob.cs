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
        float masterGrouping;

        public void Execute()
        {
            masterGrouping = topologyUtil.CellGrouping(masterCell.index);

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

                    bool sloped = ( topologyUtil.EdgeIsSloped(blWorley) || 
                                    topologyUtil.EdgeIsSloped(tlWorley) || 
                                    topologyUtil.EdgeIsSloped(trWorley) || 
                                    topologyUtil.EdgeIsSloped(brWorley) );

                    if(northWestToSouthEast)
                    {
                        if(sloped)
                        {
                            AddVertexDataQuad(blVertex, tlVertex, trVertex, brVertex, blWorley, tlWorley, trWorley, brWorley);
                        }
                        else
                        {
                            AddVertexDataForTriangle(blVertex, tlVertex, trVertex, blWorley, tlWorley, trWorley);
                            AddVertexDataForTriangle(blVertex, trVertex, brVertex, blWorley, trWorley, brWorley);
                        }
                    }
                    else
                    {
                        if(sloped)
                        {
                            AddVertexDataQuad(tlVertex, trVertex, brVertex, blVertex, tlWorley, trWorley, brWorley, blWorley);
                        }
                        else
                        {
                            AddVertexDataForTriangle(blVertex, tlVertex, brVertex, blWorley, tlWorley, brWorley);
                            AddVertexDataForTriangle(tlVertex, trVertex, brVertex, tlWorley, trWorley, brWorley);
                        }
                    } 
                }
        }

        Vertex GetVertex(int2 matrixPosition)
        {
            TopologySystem.Height terrainHeight = matrix.GetItem<TopologySystem.Height>(matrixPosition, pointHeight, arrayUtil);
            return new Vertex() { vertex = new float3(matrixPosition.x, terrainHeight.height, matrixPosition.y) };
        }

        void AddVertexDataForTriangle(Vertex a, Vertex b, Vertex c, WorleyNoise.PointData aWorley, WorleyNoise.PointData bWorley, WorleyNoise.PointData cWorley)
        {
            if(!CurrentCellIsOwner(aWorley, bWorley, cWorley)) return;

            float difference = LargestHeightDifference(a.vertex.y, b.vertex.y, c.vertex.y);
            float4 color = PointColor(difference);

            AddVertexData(a, color - new float4(aWorley.distance2Edge * 0.1));
            AddVertexData(b, color - new float4(bWorley.distance2Edge * 0.1));
            AddVertexData(c, color - new float4(cWorley.distance2Edge * 0.1));

            AddIndicesForTriangle();

            return;
        }
        void AddVertexDataQuad(Vertex a, Vertex b, Vertex c, Vertex d, WorleyNoise.PointData aWorley, WorleyNoise.PointData bWorley, WorleyNoise.PointData cWorley, WorleyNoise.PointData dWorley)
        {
            if(!CurrentCellIsOwner(aWorley, bWorley, cWorley, dWorley)) return;

            float difference = LargestHeightDifference(a.vertex.y, b.vertex.y, c.vertex.y, d.vertex.y);
            float4 color = PointColor(difference);

            bool cliff = difference > 2;

            AddVertexData(a, color - new float4(aWorley.distance2Edge * 0.1));
            AddVertexData(b, color - new float4(bWorley.distance2Edge * 0.1));
            AddVertexData(c, color - new float4(cWorley.distance2Edge * 0.1));
            AddVertexData(d, color - new float4(dWorley.distance2Edge * 0.1));

            AddIndicesForQuad();

            return;
        }

        void AddVertexData(Vertex vertex, float4 color)
        {
            vertices.Add(vertex);
            colors.Add(new VertColor{ color = color });
        }

        bool CurrentCellIsOwner(WorleyNoise.PointData aWorley, WorleyNoise.PointData bWorley, WorleyNoise.PointData cWorley)
        {
            if( aWorley.isSet == 0 ||
                bWorley.isSet == 0 ||
                cWorley.isSet == 0 )
                return false;

            NativeArray<WorleyNoise.PointData> sortPoints = new NativeArray<WorleyNoise.PointData>(3, Allocator.Temp);
            sortPoints[0] = aWorley;
            sortPoints[1] = bWorley;
            sortPoints[2] = cWorley;

            sortPoints.Sort();
            float ownerGrouping = topologyUtil.CellGrouping(sortPoints[0].currentCellIndex);
            
            return masterGrouping == ownerGrouping;
        }
        bool CurrentCellIsOwner(WorleyNoise.PointData aWorley, WorleyNoise.PointData bWorley, WorleyNoise.PointData cWorley, WorleyNoise.PointData dWorley)
        {
            if( aWorley.isSet == 0 ||
                bWorley.isSet == 0 ||
                cWorley.isSet == 0 ||
                dWorley.isSet == 0 )
                return false;

            NativeArray<WorleyNoise.PointData> sortPoints = new NativeArray<WorleyNoise.PointData>(4, Allocator.Temp);
            sortPoints[0] = aWorley;
            sortPoints[1] = bWorley;
            sortPoints[2] = cWorley;
            sortPoints[3] = dWorley;

            sortPoints.Sort();
            float ownerGrouping = topologyUtil.CellGrouping(sortPoints[0].currentCellIndex);
            
            return masterGrouping == ownerGrouping;
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

        void AddIndicesForTriangle()
        {
            triangles.Add(new Triangle{ triangle = 0 + indexOffset });
            triangles.Add(new Triangle{ triangle = 1 + indexOffset });
            triangles.Add(new Triangle{ triangle = 2 + indexOffset });

            indexOffset += 3;
        }
        void AddIndicesForQuad()
        {
            triangles.Add(new Triangle{ triangle = 0 + indexOffset });
            triangles.Add(new Triangle{ triangle = 1 + indexOffset });
            triangles.Add(new Triangle{ triangle = 2 + indexOffset });
            triangles.Add(new Triangle{ triangle = 0 + indexOffset });
            triangles.Add(new Triangle{ triangle = 2 + indexOffset });
            triangles.Add(new Triangle{ triangle = 3 + indexOffset });
            
            indexOffset += 4;
        }

        float4 PointColor(float difference)
        {
            float4 grey = new float4(0.6f, 0.6f, 0.6f, 1);
            float4 green = new float4(0.2f, 0.6f, 0.1f, 1);

            bool rockyBiome = (sectorType == SectorSystem.SectorTypes.GULLY || sectorType == SectorSystem.SectorTypes.MOUNTAIN || sectorType == SectorSystem.SectorTypes.LAKE);

            if(rockyBiome || difference > 2)
                return grey;
            else
                return green;
        }
    }
}
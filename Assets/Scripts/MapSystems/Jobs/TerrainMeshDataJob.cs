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

                    WorleyNoise.PointData blWorley = matrix.GetItem<WorleyNoise.PointData>(bl, worley, arrayUtil);
                    WorleyNoise.PointData tlWorley = matrix.GetItem<WorleyNoise.PointData>(tl, worley, arrayUtil);
                    WorleyNoise.PointData trWorley = matrix.GetItem<WorleyNoise.PointData>(tr, worley, arrayUtil);
                    WorleyNoise.PointData brWorley = matrix.GetItem<WorleyNoise.PointData>(br, worley, arrayUtil);

                    if( blWorley.isSet == 0 ||
                        tlWorley.isSet == 0 ||
                        trWorley.isSet == 0 ||
                        brWorley.isSet == 0 )
                    {
                        continue;
                    }

                    TopologySystem.Height blHeight = matrix.GetItem<TopologySystem.Height>(bl, pointHeight, arrayUtil);
                    TopologySystem.Height tlHeight = matrix.GetItem<TopologySystem.Height>(tl, pointHeight, arrayUtil);
                    TopologySystem.Height trHeight = matrix.GetItem<TopologySystem.Height>(tr, pointHeight, arrayUtil);
                    TopologySystem.Height brHeight = matrix.GetItem<TopologySystem.Height>(br, pointHeight, arrayUtil);

                    bool northWestToSouthEast = NorthWestToSouthEast(blWorley, tlWorley, trWorley, brWorley);

                    if(northWestToSouthEast)
                    {
                        AddVerticesForTriangle(bl, tl, tr, blHeight.height, tlHeight.height, trHeight.height);
                        AddVerticesForTriangle(bl, tr, br, blHeight.height, trHeight.height, brHeight.height);
                    }
                    else
                    {
                        AddVerticesForTriangle(bl, tl, br, blHeight.height, tlHeight.height, brHeight.height);
                        AddVerticesForTriangle(tl, tr, br, tlHeight.height, trHeight.height, brHeight.height);
                    } 

                    GetTriangleIndicesForQuad(indexOffset, northWestToSouthEast);

                    indexOffset += 6;
                }
        }

        bool NorthWestToSouthEast(WorleyNoise.PointData bl, WorleyNoise.PointData tl, WorleyNoise.PointData tr, WorleyNoise.PointData br)
        {
            int blHeight = (int)topologyUti.CellHeight(bl.currentCellIndex);
            int tlHeight = (int)topologyUti.CellHeight(tl.currentCellIndex);
            int trHeight = (int)topologyUti.CellHeight(tr.currentCellIndex);
            int brHeight = (int)topologyUti.CellHeight(br.currentCellIndex);

            if(blHeight != trHeight)
                return false;
            else
                return true;
        }

        void AddVerticesForTriangle(int2 a, int2 b, int2 c, float aHeight, float bHeight, float cHeight)
        {
            vertices.Add(new Vertex{ vertex = new float3(a.x, aHeight, a.y) });
            vertices.Add(new Vertex{ vertex = new float3(b.x, bHeight, b.y) });
            vertices.Add(new Vertex{ vertex = new float3(c.x, cHeight, c.y) });

            float difference = LargestHeightDifference(aHeight, bHeight, cHeight);
            colors.Add(new VertColor{ color = PointColor(difference) });
            colors.Add(new VertColor{ color = PointColor(difference) });
            colors.Add(new VertColor{ color = PointColor(difference) });
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

            if(sectorType == SectorSystem.SectorTypes.MOUNTAIN || difference > 1)
            {
                return grey;
            }
            else
            {
                return green;
            }
        }

        void GetTriangleIndicesForQuad(int indexOffset, bool slopeAngle)
        {
            triangles.Add(new Triangle{ triangle = 0 + indexOffset });
            triangles.Add(new Triangle{ triangle = 1 + indexOffset });
            triangles.Add(new Triangle{ triangle = 2 + indexOffset });
            triangles.Add(new Triangle{ triangle = 3 + indexOffset });
            triangles.Add(new Triangle{ triangle = 4 + indexOffset });
            triangles.Add(new Triangle{ triangle = 5 + indexOffset });
        }


    }
}
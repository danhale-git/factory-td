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

    EntityQuery meshDataGroup;

    TopologyUtil biomes;

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;

        biomes = new TopologyUtil();

        EntityQueryDesc meshDataQuery = new EntityQueryDesc{
            All = new ComponentType[] { typeof(WorleyNoise.CellData), typeof(TopologySystem.Height) },
            None = new ComponentType[] { typeof(Unity.Rendering.RenderMesh), typeof(Vertex) }
        };
        meshDataGroup = GetEntityQuery(meshDataQuery);
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
        ArchetypeChunkComponentType<CellSystem.MatrixComponent> matrixType = GetArchetypeChunkComponentType<CellSystem.MatrixComponent>(true);
        ArchetypeChunkComponentType<WorleyNoise.CellData> cellType = GetArchetypeChunkComponentType<WorleyNoise.CellData>(true);

        ArchetypeChunkBufferType<WorleyNoise.PointData> worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);
        ArchetypeChunkBufferType<TopologySystem.Height> topologyType = GetArchetypeChunkBufferType<TopologySystem.Height>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<CellSystem.MatrixComponent> matrices = chunk.GetNativeArray(matrixType);
            NativeArray<WorleyNoise.CellData> cells = chunk.GetNativeArray(cellType);

            BufferAccessor<WorleyNoise.PointData> worleyBuffers = chunk.GetBufferAccessor(worleyType);
            BufferAccessor<TopologySystem.Height> TopologyBuffers = chunk.GetBufferAccessor(topologyType);

            for(int e = 0; e < entities.Length; e++)
            {
                ArrayUtil arrayUtil = new ArrayUtil();

                Entity entity = entities[e];
                CellSystem.MatrixComponent matrix = matrices[e];
                WorleyNoise.CellData cell = cells[e];

                DynamicBuffer<WorleyNoise.PointData> worley = worleyBuffers[e];
                DynamicBuffer<TopologySystem.Height> topology = TopologyBuffers[e];

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

                        WorleyNoise.PointData bottomLeftWorley  = matrix.GetItem<WorleyNoise.PointData>(bl, worley, arrayUtil);
                        WorleyNoise.PointData topLeftWorley     = matrix.GetItem<WorleyNoise.PointData>(tl, worley, arrayUtil);
                        WorleyNoise.PointData topRightWorley    = matrix.GetItem<WorleyNoise.PointData>(tr, worley, arrayUtil);
                        WorleyNoise.PointData bottomRightWorley = matrix.GetItem<WorleyNoise.PointData>(br, worley, arrayUtil);
                        
                        if( bottomLeftWorley.isSet  == 0 ||
                            topLeftWorley.isSet     == 0 ||
                            topRightWorley.isSet    == 0 ||
                            bottomRightWorley.isSet == 0 )
                        {
                            continue;
                        }

                        TopologySystem.Height bottomLeft            = matrix.GetItem<TopologySystem.Height>(bl, topology, arrayUtil);
                        TopologySystem.Height topLeftTopology       = matrix.GetItem<TopologySystem.Height>(tl, topology, arrayUtil);
                        TopologySystem.Height topRightTopology      = matrix.GetItem<TopologySystem.Height>(tr, topology, arrayUtil);
                        TopologySystem.Height bottomRightTopology   = matrix.GetItem<TopologySystem.Height>(br, topology, arrayUtil);

                        vertices.Add(new Vertex{ vertex = new float3(bl.x, bottomLeft.height, bl.y) });
                        vertices.Add(new Vertex{ vertex = new float3(tl.x, topLeftTopology.height, tl.y) });
                        vertices.Add(new Vertex{ vertex = new float3(tr.x, topRightTopology.height, tr.y) });
                        vertices.Add(new Vertex{ vertex = new float3(br.x, bottomRightTopology.height, br.y) });

                        triangles.Add(new Triangle{ triangle = 0 + indexOffset });
                        triangles.Add(new Triangle{ triangle = 1 + indexOffset });
                        triangles.Add(new Triangle{ triangle = 2 + indexOffset });
                        triangles.Add(new Triangle{ triangle = 0 + indexOffset });
                        triangles.Add(new Triangle{ triangle = 2 + indexOffset });
                        triangles.Add(new Triangle{ triangle = 3 + indexOffset });

                        WorleyNoise.PointData worleyPoint = matrix.GetItem<WorleyNoise.PointData>(bl, worley, arrayUtil);
                        float difference = LargestHeightDifference(bottomLeft.height, topLeftTopology.height, topRightTopology.height, bottomRightTopology.height);
                        float4 color = DebugTerrainColor(worleyPoint, cell, difference, new float3(x, 0, z) + matrix.root, entity);

                        colors.Add(new VertColor{ color = color });
                        colors.Add(new VertColor{ color = color });
                        colors.Add(new VertColor{ color = color });
                        colors.Add(new VertColor{ color = color });

                        indexOffset += 4;

                        /*if(entityManager.GetComponentData<SectorSystem.SectorType>(entity).Value == SectorSystem.SectorTypes.LAKE)
                        {
                            float waterHeight = biomes.CellHeight(cell.index) - 0.1f;
                            vertices.Add(new Vertex{ vertex = new float3(bl.x, waterHeight, bl.y) });
                            vertices.Add(new Vertex{ vertex = new float3(tl.x, waterHeight, tl.y) });
                            vertices.Add(new Vertex{ vertex = new float3(tr.x, waterHeight, tr.y) });
                            vertices.Add(new Vertex{ vertex = new float3(br.x, waterHeight, br.y) });

                            triangles.Add(new Triangle{ triangle = 0 + indexOffset });
                            triangles.Add(new Triangle{ triangle = 1 + indexOffset });
                            triangles.Add(new Triangle{ triangle = 2 + indexOffset });
                            triangles.Add(new Triangle{ triangle = 0 + indexOffset });
                            triangles.Add(new Triangle{ triangle = 2 + indexOffset });
                            triangles.Add(new Triangle{ triangle = 3 + indexOffset });

                            color = new float4(0.2f, 0.7f, 0.9f, 0.3f);

                            colors.Add(new VertColor{ color = color });
                            colors.Add(new VertColor{ color = color });
                            colors.Add(new VertColor{ color = color });
                            colors.Add(new VertColor{ color = color });
    
                            indexOffset += 4;
                        } */
                    }
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    float LargestHeightDifference(float a, float b, float c, float d)
    {
        float largest = math.max(a, math.max(b, math.max(c, d)));
        float smallest = math.min(a, math.min(b, math.min(c, d)));
        return largest - smallest;
    }

    float4 DebugTerrainColor(WorleyNoise.PointData point, WorleyNoise.CellData cell, float difference, float3 worldPosition, Entity entity)
    {
        float4 color;
        
        float distance = point.distance2Edge;

        if(math.round(difference) > 1) color = new float4(0.7f, 0.7f, 0.7f, 1);
        else color = (entityManager.GetComponentData<SectorSystem.SectorNoiseValue>(entity).Value);
        //else color = new float4(0.2f, 0.8f, 0.1f, 1);
        
        color -= new float4(distance/2, distance/2, distance/2, 1); 

        if(worldPosition.x == cell.position.x && worldPosition.z == cell.position.z)
            color = new float4(1, 0, 0, 1);

        //float heightColor = bottomLeft.height / TerrainSettings.heightMultiplier;
        //color = new float4(heightColor, heightColor,heightColor, 1);

        if(entityManager.GetComponentData<SectorSystem.TypeComponent>(entity).Value == SectorSystem.SectorTypes.UNPATHABLE)
            color += new float4(0.5f,0,0,1);

        /*int2 adjacentDirection = worleyPoint.adjacentCellIndex - worleyPoint.currentCellIndex;
        if(biomes.EdgeIsSloped(adjacentDirection, worleyPoint.currentCellValue, worleyPoint.adjacentCellValue))
            color += new float4(0, 0.5f, 0.5f, 1);  */

        return color;
    }
}
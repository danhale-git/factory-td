using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;

using ECSMesh;
using MapGeneration;

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

        var entityType = GetArchetypeChunkEntityType();
        var matrixType = GetArchetypeChunkComponentType<CellSystem.MatrixComponent>(true);
        var cellType = GetArchetypeChunkComponentType<WorleyNoise.CellData>(true);

        var worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);
        var topologyType = GetArchetypeChunkBufferType<TopologySystem.Height>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<CellSystem.MatrixComponent> matrices = chunk.GetNativeArray(matrixType);
            NativeArray<WorleyNoise.CellData> cells = chunk.GetNativeArray(cellType);

            BufferAccessor<WorleyNoise.PointData> worleyArrays = chunk.GetBufferAccessor(worleyType);
            BufferAccessor<TopologySystem.Height> topologyArrays = chunk.GetBufferAccessor(topologyType);

            for(int e = 0; e < entities.Length; e++)
            {

                TerrainMeshDataJob job = new TerrainMeshDataJob{
                    commandBuffer = commandBuffer,
                    sectorEntity = entities[e],
                    matrix = matrices[e],
                    worley = worleyArrays[e],
                    topology = topologyArrays[e],
                    arrayUtil = new ArrayUtil()
                }; 
                job.Schedule().Complete();
                
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    float4 DebugTerrainColor(WorleyNoise.PointData point, WorleyNoise.CellData cell, float difference, float3 worldPosition, Entity entity)
    {
        float4 color;
        
        float distance = point.distance2Edge;

        if(math.round(difference) > 1) color = new float4(0.7f, 0.7f, 0.7f, 1);
        //else color = (entityManager.GetComponentData<SectorSystem.SectorNoiseValue>(entity).Value);
        else color = new float4(0.2f, 0.8f, 0.1f, 1);
        
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
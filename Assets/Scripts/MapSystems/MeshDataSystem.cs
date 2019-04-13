﻿using UnityEngine;

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

    ASyncJobManager jobManager;

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

    protected override void OnDestroy()
    {
        jobManager.Dispose();
    }

    protected override void OnUpdate()
    {
        if(!jobManager.AllJobsCompleted())
            return;
        else
            ScheduleMeshDataJobs();
    }

    void ScheduleMeshDataJobs()
    {
        NativeArray<ArchetypeChunk> chunks = meshDataGroup.CreateArchetypeChunkArray(Allocator.TempJob);

        var entityType = GetArchetypeChunkEntityType();
        var matrixType = GetArchetypeChunkComponentType<CellSystem.MatrixComponent>(true);
        var sectorType = GetArchetypeChunkComponentType<SectorSystem.TypeComponent>(true);

        var worleyType = GetArchetypeChunkBufferType<WorleyNoise.PointData>(true);
        var topologyType = GetArchetypeChunkBufferType<TopologySystem.Height>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<CellSystem.MatrixComponent> matrices = chunk.GetNativeArray(matrixType);
            NativeArray<SectorSystem.TypeComponent> sectorTypes = chunk.GetNativeArray(sectorType);

            BufferAccessor<WorleyNoise.PointData> worleyArrays = chunk.GetBufferAccessor(worleyType);
            BufferAccessor<TopologySystem.Height> topologyArrays = chunk.GetBufferAccessor(topologyType);

            for(int e = 0; e < entities.Length; e++)
            {
                var worley = new NativeArray<WorleyNoise.PointData>(topologyArrays[e].Length, Allocator.TempJob);
                var height = new NativeArray<TopologySystem.Height>(topologyArrays[e].Length, Allocator.TempJob);

                worley.CopyFrom(worleyArrays[e].AsNativeArray());
                height.CopyFrom(topologyArrays[e].AsNativeArray());

                TerrainMeshDataJob job = new TerrainMeshDataJob{
                    commandBuffer = jobManager.commandBuffer,
                    sectorEntity = entities[e],
                    sectorType = sectorTypes[e].Value,
                    matrix = matrices[e],
                    worley = worley,
                    pointHeight = height,
                    arrayUtil = new ArrayUtil()
                }; 
                
                jobManager.ScheduleNewJob(job);
            }
        }

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

        if(entityManager.GetComponentData<SectorSystem.TypeComponent>(entity).Value == SectorSystem.SectorTypes.MOUNTAIN)
            color += new float4(0.5f,0,0,1);

        /*int2 adjacentDirection = worleyPoint.adjacentCellIndex - worleyPoint.currentCellIndex;
        if(biomes.EdgeIsSloped(adjacentDirection, worleyPoint.currentCellValue, worleyPoint.adjacentCellValue))
            color += new float4(0, 0.5f, 0.5f, 1);  */

        return color;
    }
}
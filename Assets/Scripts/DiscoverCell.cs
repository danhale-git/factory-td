using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;
using Unity.Rendering;

[AlwaysUpdateSystem]
public class DiscoverCell : ComponentSystem
{
    EntityManager entityManager;

    WorleyNoise worley;
    float cellValue;

    Matrix<int> matrix;

    EntityArchetype cellArchetype;

    DynamicBuffer<WorleyNoise.PointData> worleyBuffer;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        worley = new WorleyNoise(
            TerrainSettings.seed,
            TerrainSettings.cellFrequency,
            TerrainSettings.cellEdgeSmoothing,
            TerrainSettings.cellularJitter
        );

        cellArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<Translation>(),
            ComponentType.ReadWrite<RenderMeshProxy>(),
            ComponentType.ReadWrite<WorleyNoise.PointData>()
        );




        matrix = new Matrix<int>(10, Allocator.Persistent, float3.zero);

        cellValue = worley.GetPointData(0,0).currentCellValue;

        //Testing(float3.zero);

        Entity cell = CreateCellEntity(float3.zero);

        worleyBuffer = entityManager.GetBuffer<WorleyNoise.PointData>(cell);
        Discover(float3.zero, worleyBuffer);

        for(int i = 0; i < worleyBuffer.Length; i++)
            CreatePlane(worleyBuffer[i].pointWorldPosition);
    }

    protected override void OnDestroyManager()
    {
        matrix.Dispose();
    }

    protected override void OnUpdate()
    {
        
    }

    void Discover(float3 position, DynamicBuffer<WorleyNoise.PointData> buffer)
    {
        WorleyNoise.PointData data = worley.GetPointData(position.x, position.z);
        data.pointWorldPosition = position;

        if(matrix.ItemIsSet(position) || data.currentCellValue != cellValue)
            return;

        buffer.Add(data);

        matrix.AddItem(1, position);

        for(int x = -1; x <= 1; x++)
            for(int z = -1; z <= 1; z++)
            {
                if(x + z == 0) continue;

                float3 adjacent = new float3(x, 0, z) + position;

                Discover(adjacent, buffer);
            }
    }

    GameObject CreatePlane(float3 position)
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.localScale = new float3(0.1f);
        plane.transform.Translate(position);
        return plane;
    }

    Entity CreateCellEntity(float3 worldPosition)
    {
        Entity entity = entityManager.CreateEntity(cellArchetype);
        entityManager.SetComponentData<Translation>(entity, new Translation{ Value = worldPosition } );
        return entity;
    }
}

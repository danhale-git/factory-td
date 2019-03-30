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

    Matrix<WorleyNoise.PointData> matrix;

    EntityArchetype cellArchetype;

    struct CellMatrix<T> : IComponentData where T : struct, IComponentData
    {
        public float3 root;
        public int width;

        public T GetItem(float3 worlPosition, DynamicBuffer<T> data, ArrayUtil util)
        {
            int index = util.Flatten2D(worlPosition - root, width);
            return data[index];
        }
    }

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




        matrix = new Matrix<WorleyNoise.PointData>(10, Allocator.Persistent, float3.zero);

        cellValue = worley.GetPointData(0,0).currentCellValue;

        //Testing(float3.zero);

        Entity cell = CreateCellEntity(float3.zero);

        DynamicBuffer<WorleyNoise.PointData> worleyBuffer = entityManager.GetBuffer<WorleyNoise.PointData>(cell);
        Discover(float3.zero);

        worleyBuffer.CopyFrom(matrix.matrix);

        for(int i = 0; i < worleyBuffer.Length; i++)
            if(worleyBuffer[i].isSet > 0)
                CreatePlane(worleyBuffer[i].pointWorldPosition);
    }

    protected override void OnDestroyManager()
    {
        matrix.Dispose();
    }

    protected override void OnUpdate()
    {
        
    }

    void Discover(float3 position)
    {
        WorleyNoise.PointData data = worley.GetPointData(position.x, position.z);
        data.pointWorldPosition = position;
        data.isSet = 1;

        if(matrix.ItemIsSet(position) || data.currentCellValue != cellValue)
            return;

        matrix.AddItem(data, position);

        for(int x = -1; x <= 1; x++)
            for(int z = -1; z <= 1; z++)
            {
                if(x + z == 0) continue;

                float3 adjacent = new float3(x, 0, z) + position;

                Discover(adjacent);
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

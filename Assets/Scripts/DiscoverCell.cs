using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

public class DiscoverCell : ComponentSystem
{
    WorleyNoise worley;
    float cellValue;

    Matrix<int> matrix;
    
    protected override void OnCreateManager()
    {
        worley = new WorleyNoise(
            TerrainSettings.seed,
            TerrainSettings.cellFrequency,
            TerrainSettings.cellEdgeSmoothing,
            TerrainSettings.cellularJitter
        );

        matrix = new Matrix<int>(10, Allocator.Persistent, float3.zero);

        cellValue = worley.GetPointData(0,0).currentCellValue;

        Testing(float3.zero);
    }

    protected override void OnDestroyManager()
    {
        matrix.Dispose();
    }

    protected override void OnUpdate()
    {
        
    }

    void Testing(float3 position)
    {
        WorleyNoise.PointData data = worley.GetPointData(position.x, position.z);

        if(matrix.ItemIsSet(position) || data.currentCellValue != cellValue)
            return;

        GameObject testObject = CreatePlane();
        matrix.AddItem(testObject.GetInstanceID(), position);
        testObject.transform.Translate(position);

        for(int x = -1; x <= 1; x++)
            for(int z = -1; z <= 1; z++)
            {
                if(x + z == 0) continue;

                float3 adjacent = new float3(x, 0, z) + position;

                Testing(adjacent);
            }
    }

    GameObject CreatePlane()
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.localScale = new float3(0.1f);
        return plane;
    }
}

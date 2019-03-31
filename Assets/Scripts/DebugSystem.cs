using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;

[AlwaysUpdateSystem]
public class DebugSystem : ComponentSystem
{
    static List<float3> cubePositions = new List<float3>();
    static List<float4> cubeColors = new List<float4>();

    static DebugMonoBehaviour monoBehaviour;

    protected override void OnCreateManager()
    {
        monoBehaviour = GameObject.FindObjectOfType<DebugMonoBehaviour>();
    }

    protected override void OnUpdate()
    {
        for(int i = 0; i < cubePositions.Count; i++)
        {
            CreateCube(cubePositions[i], cubeColors[i]);
        }
        cubePositions.Clear();
        cubeColors.Clear();
    }

    public static void Text(string key, string value)
    {
        monoBehaviour.debugTextEntries[key] = value;
    }
    public static void Count(string key)
    {
        int currenCount = 0;
        monoBehaviour.debugTextCountEntries.TryGetValue(key, out currenCount);
        monoBehaviour.debugTextCountEntries[key] = currenCount + 1;
    }

    public static void Cube(float3 position, float4 color)
    {
        cubePositions.Add(position);
        cubeColors.Add(color);
    }

    void CreateCube(float3 position, float4 c)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.Translate(position);
        cube.GetComponent<MeshRenderer>().material.color = new Color(c.x, c.y, c.z, c.w);
    }

}
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;

[AlwaysUpdateSystem]
public class DebugSystem : ComponentSystem
{
    PlayerEntitySystem playerSystem;

    WorleyNoise debugWorley;
    TopologyUtil topologyUtil;

    static List<float3> cubePositions = new List<float3>();
    static List<float4> cubeColors = new List<float4>();

    static DebugMonoBehaviour monoBehaviour;

    protected override void OnCreateManager()
    {
        monoBehaviour = GameObject.FindObjectOfType<DebugMonoBehaviour>();
        playerSystem = World.Active.GetOrCreateManager<PlayerEntitySystem>();
        debugWorley = TerrainSettings.CellWorley();
        topologyUtil = new TopologyUtil();
    }

    protected override void OnUpdate()
    {
        for(int i = 0; i < cubePositions.Count; i++)
        {
            CreateCube(cubePositions[i], cubeColors[i]);
        }
        cubePositions.Clear();
        cubeColors.Clear();

        DebugWorley();
    }

    public static void Text(string key, string value)
    {
        if(monoBehaviour == null) return;
        monoBehaviour.debugTextEntries[key] = value;
    }
    public static void Count(string key)
    {
        if(monoBehaviour == null) return;
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

    void DebugWorley()
    {
        if(playerSystem.player == null) return;
        float3 playerPosition = math.round(playerSystem.player.transform.position);
        WorleyNoise.PointData point = debugWorley.GetPointData(playerPosition.x, playerPosition.z);
        Text("distance2Edge", point.distance2Edge.ToString());
        Text("CURRENT", point.currentCellIndex.ToString());
        Text("ADJACENT", point.adjacentCellIndex.ToString());
        Text("group", topologyUtil.CellGrouping(point.currentCellIndex).ToString());
    }

}
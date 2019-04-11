using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;

[AlwaysUpdateSystem]
public class DebugSystem : ComponentSystem
{
    EntityManager entityManager;
    PlayerEntitySystem playerSystem;
    CellSystem cellSystem;

    WorleyNoise debugWorley;
    TopologyUtil topologyUtil;

    static List<float3> cubePositions = new List<float3>();
    static List<float4> cubeColors = new List<float4>();

    static DebugMonoBehaviour monoBehaviour;

    GameObject worleyCurrentMarker;
    GameObject worleyAdjacentMarker;

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        playerSystem = World.Active.GetOrCreateSystem<PlayerEntitySystem>();
        cellSystem = World.Active.GetOrCreateSystem<CellSystem>();
        monoBehaviour = GameObject.FindObjectOfType<DebugMonoBehaviour>();
        debugWorley = TerrainSettings.CellWorley();
        topologyUtil = new TopologyUtil();

        worleyCurrentMarker = CreateCube(float3.zero, new float4(0, 1, 0, 1));
        worleyAdjacentMarker = CreateCube(float3.zero, new float4(0, 0, 1, 1));
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

    GameObject CreateCube(float3 position, float4 c)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.Translate(position);
        cube.GetComponent<MeshRenderer>().material.color = new Color(c.x, c.y, c.z, c.w);
        return cube;
    }

    void DebugWorley()
    {
        if(playerSystem.player == null) return;
        float3 playerPosition = math.round(playerSystem.player.transform.position);
        WorleyNoise.PointData point = debugWorley.GetPointData(playerPosition.x, playerPosition.z);
        Text("distance2Edge", point.distance2Edge.ToString());

        Entity cellEntity;
        if(cellSystem.TryGetCell(point.currentCellIndex, out cellEntity) && entityManager.HasComponent<SectorSystem.SectorNoiseValue>(cellEntity))
            Text("group", topologyUtil.CellGrouping(point.currentCellIndex).ToString()+" "+entityManager.GetComponentData<SectorSystem.SectorNoiseValue>(cellEntity).Value);

        worleyCurrentMarker.transform.position = math.round(point.currentCellPosition) + new float3(0.5f, cellSystem.GetHeightAtPosition(point.currentCellPosition)+1, 0.5f);

        worleyAdjacentMarker.transform.position = math.round(point.adjacentCellPosition) + new float3(0.5f, cellSystem.GetHeightAtPosition(point.adjacentCellPosition)+1, 0.5f);
    }

}
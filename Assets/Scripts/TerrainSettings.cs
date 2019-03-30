using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TerrainTypes { DIRT, GRASS, CLIFF }

public static class TerrainSettings
{
	public const int mapSquareWidth = 12;
	public const int viewDistance = 8;
	public const int cellGenerateDistance = 3;

	//	Must always be at >= squareWidth
	public const int terrainHeight = 16;
	public const int seed = 5678;

	public const float cellFrequency = 0.05f;
	//public const float cellEdgeSmoothing = 10.0f;
	public const float cellEdgeSmoothing = 0;
	public const float cellularJitter = 0.15f;

	public const int levelHeight = 5;
    public const float cliffDepth = 0.05f;
    public const int levelCount = 5;

	public static int BiomeIndex(float noise)
	{
		if(noise > 0.5f) return 1;
		return 0;
	}
}
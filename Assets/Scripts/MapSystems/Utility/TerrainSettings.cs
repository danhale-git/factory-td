using System.Collections;
using System.Collections.Generic;
//using UnityEngine;
using Unity.Mathematics;

public enum TerrainTypes { DIRT, GRASS, CLIFF }

public static class TerrainSettings
{
	static Random random = new Unity.Mathematics.Random(seed);	

	public const int mapSquareWidth = 12;
	public const int viewDistance = 8;
	public const int cellGenerateDistance = 3;

	public const int terrainHeight = 16;
	public const int seed = 5678;

	public const float cellFrequency = 0.03f;
	public const float cellEdgeSmoothing = 0;
	public const float cellularJitter = 0.3f;

    public const float cellheightMultiplier = 3f;
	public const float cellHeightNoiseFrequency = 0.6f;	
	public const int cellHeightLevelCount = 4;

	public const float cellGroupNoiseFrequency = 0.4f;	
	public const int cellGroupCount = 4;

	public const float slopeLength = 0.45f;

	public const WorleyNoise.Distance2EdgeBorder cellDistanceBorder = WorleyNoise.Distance2EdgeBorder.Sector;
	public const WorleyNoise.DistanceFunction cellDistanceFunction = WorleyNoise.DistanceFunction.Natural;
	public const WorleyNoise.CellularReturnType cellReturnType = WorleyNoise.CellularReturnType.Distance2Sub;

	public const int levelHeight = 5;
    public const float cliffDepth = 0.05f;
    public const int levelCount = 5;

	public static int BiomeIndex(float noise)
	{
		if(noise > 0.5f) return 1;
		return 0;
	}

	static int heightSeed = random.NextInt();
	public static SimplexNoise HeightSimplex()
	{
		return new SimplexNoise(heightSeed, TerrainSettings.cellHeightNoiseFrequency);
	}

	static int groupSeed = random.NextInt();
	public static SimplexNoise GroupSimplex()
	{
		return new SimplexNoise(groupSeed, TerrainSettings.cellGroupNoiseFrequency);
	}

	public static WorleyNoise CellWorley()
	{
		return new WorleyNoise(
            seed,
            cellFrequency,
            cellEdgeSmoothing,
            cellularJitter,
            cellDistanceFunction,
            cellReturnType,
			cellDistanceBorder
        );
	}
}
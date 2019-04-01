using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;


public struct WorleyNoise
{
	int seed;
	float frequency;
	float perterbAmp;
	float cellularJitter;
	
	Biomes biomeIndex;
    CELL_2D cell_2D;

    int X_PRIME;
	int Y_PRIME;

	[InternalBufferCapacity(0)]
	public struct PointData : IBufferElementData, System.IComparable<PointData>
	{
		public int CompareTo(PointData other)
		{ return currentCellValue.CompareTo(other.currentCellValue); }

		public sbyte isSet;

		public float3 pointWorldPosition;

		public float3 currentCellPosition, adjacentCellPosition;
		public int2 currentCellIndex, adjacentCellIndex;
		public float currentCellValue, distance2Edge, adjacentCellValue;
	}
	public struct CellData : IComponentData, System.IComparable<CellData>
	{
		public int CompareTo(CellData other)
		{ return value.CompareTo(other.value); }

		public float value;
		public int2 index;
		public float3 position;

		public sbyte discovered;
	}

	public WorleyNoise(int seed, float frequency, float perterbAmp, float cellularJitter)
	{
		this.seed = seed;
		this.frequency = frequency;
		this.perterbAmp = perterbAmp;
		this.cellularJitter = cellularJitter;

		biomeIndex = new Biomes();
		cell_2D = new CELL_2D();

		X_PRIME = 1619;
		Y_PRIME = 31337;
	}

	public CellData GetCellData(int2 cellIndex)
    {
        
        float2 vec = cell_2D[Hash2D(TerrainSettings.seed, cellIndex.x, cellIndex.y) & 255];

        float cellX = cellIndex.x + vec.x * TerrainSettings.cellularJitter;
        float cellY = cellIndex.y + vec.y * TerrainSettings.cellularJitter;
		
		CellData cell = new CellData();

        cell.index = cellIndex;
        cell.position = math.round(new float3(cellX, 0, cellY) / TerrainSettings.cellFrequency);
		cell.value =  To01(ValCoord2D(TerrainSettings.seed, cellIndex.x, cellIndex.y));
		
		return cell;
    }

    public PointData GetPointData(float x, float y)
	{
		if(perterbAmp > 0)SingleGradientPerturb(seed, perterbAmp, frequency, ref x, ref y);

		x *= frequency;
		y *= frequency;

		int xr = FastRound(x);
		int yr = FastRound(y);

		float distance0 = 999999;
		float distance1 = 999999;

		//	Store distance1 index
		int xc1 = 0, yc1 = 0;

		//	Store distance0 index in case it is assigned to distance1 later
		int xc0 = 0, yc0 = 0;

		//	All adjacent cell indices and distances
		NineInts otherX = new NineInts();
		NineInts otherY = new NineInts();

		NineFloats otherDist = new NineFloats();
		for(int i = 0; i < 9; i++)
		{
			otherDist[i] = 999999;
		}

		int indexCount = 0;

		float3 currentCellPosition = float3.zero;
		int2 currentCellIndex = int2.zero;

		for (int xi = xr - 1; xi <= xr + 1; xi++)
				{
					for (int yi = yr - 1; yi <= yr + 1; yi++)
					{
						float2 vec = cell_2D[Hash2D(seed, xi, yi) & 255];

						float vecX = xi - x + vec.x * cellularJitter;
						float vecY = yi - y + vec.y * cellularJitter;

						float cellX = xi + vec.x * cellularJitter;
						float cellY = yi + vec.y * cellularJitter;

						//	Natural distance function
						//float newDistance = (math.abs(vecX) + math.abs(vecY)) + (vecX * vecX + vecY * vecY);
						
						//	Euclidean distance function
						float newDistance = newDistance = vecX * vecX + vecY * vecY;

						if(newDistance <= distance1)
						{
							if(newDistance >= distance0)
							{
								distance1 = newDistance;
								xc1 = xi;
								yc1 = yi;
							}
							else
							{
								distance1 = distance0;
								xc1 = xc0;
								yc1 = yc0;
							}
						}

						if(newDistance <= distance0)
						{
							distance0 = newDistance;
							xc0 = xi;
							yc0 = yi;

							currentCellPosition = new float3(cellX, 0, cellY) / frequency;
							currentCellIndex = new int2(xi, yi);
						}

						//	Store all adjacent cells
						otherX[indexCount] = xi;
						otherY[indexCount] = yi;
						otherDist[indexCount] = newDistance;
						indexCount++;			
					}
				}

		//	Current cell
		float currentCellValue = To01(ValCoord2D(seed, xc0, yc0));
		int currentBiome = biomeIndex.CellGrouping(currentCellValue);

		//	Final closest adjacent cell values
		float adjacentEdgeDistance = 999999;
		float adjacentCellValue = 0;
		int2 adjacentCellIndex = int2.zero;
		float3 adjacentCellPosition = float3.zero;

		//	Iterate over all adjacent cells
		for(int i = 0; i < 9; i++)
		{	
			//	Find closest cell within smoothing radius
			float dist2Edge = otherDist[i] - distance0;
			if(dist2Edge < adjacentEdgeDistance)
			{
				float otherCellValue = To01(ValCoord2D(seed, otherX[i], otherY[i]));
				int otherBiome = biomeIndex.CellGrouping(otherCellValue);

				///	Assign as final value if not current biome
				if(otherBiome != currentBiome)
				{
					adjacentEdgeDistance = dist2Edge;
					adjacentCellValue = otherCellValue;
					adjacentCellIndex = new int2(otherX[i], otherY[i]);
					adjacentCellPosition = new float3(otherX[i], 0, otherY[i]) / frequency;
				}
			}
		}
		if(adjacentEdgeDistance == 999999) adjacentEdgeDistance = 0;
		
		PointData cell = new PointData();
		
		cell.currentCellValue = currentCellValue;
		cell.distance2Edge = adjacentEdgeDistance;
		cell.adjacentCellValue = adjacentCellValue;
		cell.currentCellPosition = currentCellPosition;
		cell.currentCellIndex = currentCellIndex;
		cell.adjacentCellIndex = adjacentCellIndex;
		cell.adjacentCellPosition = adjacentCellPosition;

		//	Data for use in terrain generation
		return cell;
	}

	struct NineInts
	{
		int _0;
		int _1;
		int _2;
		int _3;
		int _4;
		int _5;
		int _6;
		int _7;
		int _8;
		
		public int this[int index]
		{
			get
			{
				switch(index)
				{
					case 0: return _0;
					case 1: return _1;
					case 2: return _2;
					case 3: return _3;
					case 4: return _4;
					case 5: return _5;
					case 6: return _6;
					case 7: return _7;
					case 8: return _8;

					default: throw new System.IndexOutOfRangeException();
				}
			}

			set
			{
				switch(index)
				{
					case 0: _0 = value; break;
					case 1: _1 = value; break;
					case 2: _2 = value; break;
					case 3: _3 = value; break;
					case 4: _4 = value; break;
					case 5: _5 = value; break;
					case 6: _6 = value; break;
					case 7: _7 = value; break;
					case 8: _8 = value; break;

					default: throw new System.IndexOutOfRangeException();
				}
			}
		}
	}

	struct NineFloats
	{
		float _0;
		float _1;
		float _2;
		float _3;
		float _4;
		float _5;
		float _6;
		float _7;
		float _8;
		
		public float this[int index]
		{
			get
			{
				switch(index)
				{
					case 0: return _0;
					case 1: return _1;
					case 2: return _2;
					case 3: return _3;
					case 4: return _4;
					case 5: return _5;
					case 6: return _6;
					case 7: return _7;
					case 8: return _8;

					default: throw new System.IndexOutOfRangeException();
				}
			}

			set
			{
				switch(index)
				{
					case 0: _0 = value; break;
					case 1: _1 = value; break;
					case 2: _2 = value; break;
					case 3: _3 = value; break;
					case 4: _4 = value; break;
					case 5: _5 = value; break;
					case 6: _6 = value; break;
					case 7: _7 = value; break;
					case 8: _8 = value; break;

					default: throw new System.IndexOutOfRangeException();
				}
			}
		}
	}

    float ValCoord2D(int seed, int x, int y)
	{
		int n = seed;
		n ^= X_PRIME * x;
		n ^= Y_PRIME * y;

		return (n * n * n * 60493) / (float)2147483648.0;
	}

	int Hash2D(int seed, int x, int y)
	{
		int hash = seed;
		hash ^= X_PRIME * x;
		hash ^= Y_PRIME * y;

		hash = hash * hash * hash * 60493;
		hash = (hash >> 13) ^ hash;

		return hash;
	}

    int FastRound(float f) { return (f >= 0) ? (int)(f + (float)0.5) : (int)(f - (float)0.5); }
	int FastFloor(float f) { return (f >= 0 ? (int)f : (int)f - 1); }

    float To01(float value)
	{
		return (value * 0.5f) + 0.5f;
	}

	void SingleGradientPerturb(int seed, float perturbAmp, float frequency, ref float x, ref float y)
	{
		float xf = x * frequency;
		float yf = y * frequency;

		int x0 = FastFloor(xf);
		int y0 = FastFloor(yf);
		int x1 = x0 + 1;
		int y1 = y0 + 1;

		float xs, ys;
		
		xs = InterpQuinticFunc(xf - x0);
		ys = InterpQuinticFunc(yf - y0);

		float2 vec0 = cell_2D[Hash2D(seed, x0, y0) & 255];
		float2 vec1 = cell_2D[Hash2D(seed, x1, y0) & 255];

		float lx0x = math.lerp(vec0.x, vec1.x, xs);
		float ly0x = math.lerp(vec0.y, vec1.y, xs);

		vec0 = cell_2D[Hash2D(seed, x0, y1) & 255];
		vec1 = cell_2D[Hash2D(seed, x1, y1) & 255];

		float lx1x = math.lerp(vec0.x, vec1.x, xs);
		float ly1x = math.lerp(vec0.y, vec1.y, xs);

		x += Lerp(lx0x, lx1x, ys) * perturbAmp;
		y += Lerp(ly0x, ly1x, ys) * perturbAmp;
	}
	private static float InterpQuinticFunc(float t) { return t * t * t * (t * (t * 6 - 15) + 10); }
	float Lerp(float a, float b, float t) { return a + t * (b - a); }
}
using Unity.Mathematics;

public struct TopologyUtil
{
    SimplexNoise groupSimplex;
    SimplexNoise heightSimplex;
    SimplexNoise slopeSimplex;

    public TopologyUtil Construct()
    {
        groupSimplex = TerrainSettings.GroupSimplex();
        heightSimplex = TerrainSettings.HeightSimplex();
        slopeSimplex = new SimplexNoise(TerrainSettings.seed, 0.1f);

        return this;
    }

    public float CellGrouping(int2 cellIndex)
    {
        float groupSimplexNoise = groupSimplex.GetSimplex(cellIndex.x, cellIndex.y);
        float grouped = (int)math.round(math.lerp(0, TerrainSettings.cellGroupCount, groupSimplexNoise));

        return grouped + (CellHeight(cellIndex) / 10);
    }

    public float CellHeight(int2 cellIndex)
    {
        return CellHeightGroup(cellIndex) * TerrainSettings.cellheightMultiplier;
    }
    
    public float CellHeightGroup(int2 cellIndex)
    {
        float simplexNoise = heightSimplex.GetSimplex(cellIndex.x, cellIndex.y);
        int group = (int)math.round(math.lerp(0, TerrainSettings.cellHeightLevelCount, simplexNoise));
        return group;
    }

    public bool EdgeIsSloped(WorleyNoise.PointData point)
    {
        int2 edge = point.adjacentCellIndex - point.currentCellIndex;
        SlopedSideDirections slopeSides = SlopedSide(point.currentCellValue, point.adjacentCellValue);
        return (slopeSides.a.Equals(edge) || slopeSides.b.Equals(edge));
    }

    SlopedSideDirections SlopedSide(float currentCellValue, float adjacentCellValue)
    {
        float firsDeterministicNoiseValue = (currentCellValue + adjacentCellValue)/2;
        float seconDeterministicNoiseValue = slopeSimplex.GetSimplex(currentCellValue, adjacentCellValue);

        int firstSideNumber = (int)math.round( math.lerp(0, 7, firsDeterministicNoiseValue) );
        int secondSideNumber = (int)math.round( math.lerp(0, 7, seconDeterministicNoiseValue) );

        if(currentCellValue > adjacentCellValue)
        {
            firstSideNumber = OppositeSideNumber(firstSideNumber);
            secondSideNumber = OppositeSideNumber(secondSideNumber);
        }

        int2 sideA = GetSideDirection(firstSideNumber);
        int2 sideB = GetSideDirection(secondSideNumber);

        return new SlopedSideDirections(sideA, sideB);
    }

    public int2 GetSideDirection(int noiseGroup)
    {
        switch(noiseGroup)
        {
            case 0:
                return new int2(0, 1);
            case 1:
                return new int2(1, 1);
            case 2:
                return new int2(1, 0);
            case 3:
                return new int2(1, -1);
            case 4:
                return new int2(0, -1);
            case 5:
                return new int2(-1, -1);
            case 6:
                return new int2(-1, 0);
            case 7:
                return new int2(-1, 1);
            default:
                throw new System.IndexOutOfRangeException();
        }
    }
    
    struct SlopedSideDirections
    {
        public SlopedSideDirections(int2 a, int2 b)
        {
            this.a = a;
            this.b = b;
        }   
        readonly public int2 a;
        readonly public int2 b;
    }

    public int OppositeSideNumber(int side)
    {
        int reversed = side + 4;
        if(reversed > 7) reversed -= 8;

        return reversed;
    }
}
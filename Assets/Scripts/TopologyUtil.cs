using Unity.Mathematics;

public struct TopologyUtil
{
    static SimplexNoiseGenerator groupSimplex = TerrainSettings.GroupSimplex();
    static SimplexNoiseGenerator heightSimplex = TerrainSettings.HeightSimplex();

    public float CellGrouping(int2 cellIndex)
    {
        float groupSimplexNoise = groupSimplex.GetSimplex(cellIndex.x, cellIndex.y);
        float grouped = (int)math.round(math.lerp(0, TerrainSettings.cellGroupCount, groupSimplexNoise));

        return grouped + (CellHeight(cellIndex) / 10);
    }

    public float CellHeight(int2 cellIndex)
    {
        float simplexNoise = heightSimplex.GetSimplex(cellIndex.x, cellIndex.y);
        int grouped = (int)math.round(math.lerp(0, TerrainSettings.cellHeightLevelCount, simplexNoise));
        return grouped * TerrainSettings.cellheightMultiplier;
    }

    public bool EdgeIsSloped(int2 edge, float currentCellValue, float adjacentCellValue)
    {
        int2 slopeSide = SlopedSide(currentCellValue, adjacentCellValue);
        return slopeSide.Equals(edge);
    }

    public int2 SlopedSide(WorleyNoise.PointData point)
    {
        return SlopedSide(point.currentCellValue, point.adjacentCellValue);
    }

    int2 SlopedSide(float currentCellValue, float adjacentCellValue)
    {
        int side = (int)math.round( math.lerp(0, 7, (currentCellValue + adjacentCellValue)/2) );

        if(currentCellValue > adjacentCellValue)
            side = ReverseSide(side);

        switch(side)
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
    public int ReverseSide(int side)
    {
        int reversed = side + 4;
        if(reversed > 7) reversed -= 8;

        return reversed;
    }
}
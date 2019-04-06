using Unity.Mathematics;

public struct Biomes
{
    public float CellGrouping(int2 cellIndex, SimplexNoiseGenerator groupSimplex, SimplexNoiseGenerator heightSimplex)
    {
        float groupSimplexNoise = groupSimplex.GetSimplex(cellIndex.x, cellIndex.y);
        float grouped = (int)math.round(math.lerp(0, TerrainSettings.cellGroupCount, groupSimplexNoise));

        return grouped + (CellHeight(cellIndex, heightSimplex) / 10);
    }

    public float CellHeight(int2 cellIndex, SimplexNoiseGenerator simplex)
    {
        float simplexNoise = simplex.GetSimplex(cellIndex.x, cellIndex.y);
        int grouped = (int)math.round(math.lerp(0, TerrainSettings.cellHeightLevelCount, simplexNoise));
        return grouped * TerrainSettings.cellheightMultiplier;
    }

    public int2 SlopedSide(WorleyNoise.PointData point)
    {
        int side = (int)math.round(math.lerp(0, 7, point.currentCellValue * point.adjacentCellValue));

        if(point.currentCellValue > point.adjacentCellValue)
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
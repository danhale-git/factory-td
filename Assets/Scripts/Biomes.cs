using Unity.Mathematics;

public struct Biomes
{
    public int CellGrouping(float cellNoise)
    {
        return (int)math.round(math.lerp(0, 5, cellNoise));
    }

    public float CellHeight(float cellNoise)
    {
        return CellGrouping(cellNoise) * 2;
    }

    /*public bool Slope(int2 a, int2 b, SimplexNoiseGenerator simplex)
    {
        int aValue = a.x + a.y;
        int bValue = b.x + b.y;

        float noise = simplex.GetSimplex(math.min(aValue, bValue), math.max(aValue, bValue));

        DebugSystem.Text("noise", noise.ToString());

        //  Every cell slopes into 2 of it's 9 neighbours

        return noise > 0.5f;
    } */

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
        if(reversed > 7) reversed -= 7;

        return reversed;
    }

    
    /*public float InterpolateBetweenCells(WorleyNoise.PointData point)
    {
        float maxDistance = DistanceBetweenPositions(point.currentCellPosition, point.adjacentCellPosition);
        float pointDistance = DistanceBetweenPositions(point.currentCellPosition, point.pointWorldPosition);

        return math.unlerp(0, maxDistance, pointDistance);
    }

    float DistanceBetweenPositions(float3 a, float3 b)
    {
        float3 d = math.abs(a - b);
        return math.sqrt(d.x*d.x + d.y*d.y + d.z*d.z);
    } */
}
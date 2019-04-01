using Unity.Mathematics;

public struct Biomes
{
    public int CellGrouping(float cellNoise)
    {
        if(cellNoise < 0.25f)
            return 0;
        if(cellNoise < 0.5f)
            return 1;
        else if(cellNoise < 0.75f)    
            return 2;
        else return 3;
    }

    public float CellHeight(int2 cellIndex, SimplexNoiseGenerator simplex)
    {
        return simplex.GetSimplex(cellIndex.x, cellIndex.y);
    }
}
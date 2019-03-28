using Unity.Mathematics;

public struct Biomes
{
    public int GetIndex(float cellNoise)
    {
        if(cellNoise > 0.5f)
            return 0;
        else    
            return 1;
    }
}
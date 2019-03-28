using Unity.Mathematics;

public struct GRAD_2D
{
    public float2 this[int index]
    {
        get
        {
            switch(index)
            {
                case 0: return new float2(-1,-1); 
                case 1: return new float2( 1,-1); 
                case 2: return new float2(-1, 1); 
                case 3: return new float2( 1, 1);
                case 4: return new float2( 0,-1); 
                case 5: return new float2(-1, 0); 
                case 6: return new float2( 0, 1); 
                case 7: return new float2( 1, 0);
                default: throw new System.IndexOutOfRangeException();
            }
        }
    }
}
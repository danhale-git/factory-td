using Unity.Mathematics;
using Unity.Collections;

public struct ArrayUtil
{ 
    public int Flatten(int x, int y, int z, int width)
    {
        return ((z * width) + x) + (y * (width * width));
    }

    public int Flatten(float x, float y, float z, int width)
    {
        return (((int)z * width) + (int)x) + ((int)y * (width * width));
    }
    
    public float3 Unflatten(int index, int width)
    {
        int y = (int)math.floor(index / (width * width));
        index -= y * (width * width);
        int z = (int)math.floor(index / width);
        int x = index - (width * z);
        return new float3(x, y, z);
    }
    
    public int Flatten2D(float x, float z, int size)
    {
        return ((int)z * size) + (int)x;
    }

    public int Flatten2D(int x, int z, int size)
    {
        return (z * size) + x;
    }

    public int Flatten2D(int2 xy, int size)
    {
        return (xy.y * size) + xy.x;
    }
    
    public int Flatten2D(float3 xyz, int size)
    {
        return ((int)xyz.z * size) + (int)xyz.x;
    }

    public float3 Unflatten2D(int index, int size)
    {
        int x = index % size;
        int z = index / size;

        return new float3(x, 0, z);
    }

    public NativeArray<T> Set<T>(NativeArray<T> raw, Allocator label) where T : struct, System.IComparable<T>
    {
        NativeList<T> set = new NativeList<T>(Allocator.Temp);

        if(raw.Length == 0) return set;

        raw.Sort();

        int index = 0;
        set.Add(raw[0]);

        for(int i = 1; i < raw.Length; i++)
        {
            if(raw[i].CompareTo(set[index]) != 0)
            {
                index++;
                set.Add(raw[i]);
            }
        }

        NativeArray<T> array = new NativeArray<T>(set.Length, label);
        array.CopyFrom(set);
        set.Dispose();
        return array;
    }
}
using Unity.Mathematics;
using System.Collections.Generic;

struct TestUtility
{
    public WorleyNoise.PointData RandomPointData(WorleyNoise cellWorley)
    {
        int x = UnityEngine.Random.Range(-5000, 5000);
        int z = UnityEngine.Random.Range(-5000, 5000);

        return cellWorley.GetPointData(x, z);
    }

    public WorleyNoise.CellData RandomCellData(WorleyNoise cellWorley)
    {
        int x = UnityEngine.Random.Range(-500, 500);
        int z = UnityEngine.Random.Range(-500, 500);

        return cellWorley.GetCellData(new int2(x, z));
    }

    public List<T> Set<T>(List<T> raw) where T : struct, System.IComparable<T>
    {
        List<T> set = new List<T>();

        if(raw.Count == 0) return set;

        raw.Sort();

        int index = 0;
        set.Add(raw[0]);

        for(int i = 1; i < raw.Count; i++)
        {
            if(raw[i].CompareTo(set[index]) != 0)
            {
                index++;
                set.Add(raw[i]);
            }
        }

        return set;
    }
}
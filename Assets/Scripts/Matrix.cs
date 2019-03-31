using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

public struct Matrix<T> where T : struct
{
    [DeallocateOnJobCompletion]
    public NativeArray<T> matrix;
    [DeallocateOnJobCompletion]
    public NativeArray<sbyte> isSet;

    public int width;
    public Allocator label;

    public float3 rootPosition;
    public int itemWorldSize;

    ArrayUtil util;

    bool job;

    public int Length{ get{ return matrix.Length; } }

    public void Dispose()
    {
        if(matrix.IsCreated) matrix.Dispose();
        if(isSet.IsCreated) isSet.Dispose();
    }

    public Matrix(int width, Allocator label, float3 rootPosition, int itemWorldSize = 1, bool job = false)
    {
        this.job = job;

        matrix = new NativeArray<T>((int)math.pow(width, 2), job ? Allocator.TempJob : label);
        isSet = new NativeArray<sbyte>(matrix.Length, job ? Allocator.TempJob : label);

        this.width = width;

        this.label = job ? Allocator.Temp : label;
        this.rootPosition = rootPosition;
        this.itemWorldSize = itemWorldSize;

        util = new ArrayUtil();
    }

    public void AddItem(T item, int2 worldPosition)
    {
        AddItem(item, new float3(worldPosition.x, 0, worldPosition.y));
    }

    public void AddItem(T item, float3 worldPosition)
    {
        if(!WorldPositionIsInMatrix(worldPosition))
            RepositionResize(WorldToMatrixPosition(worldPosition));

        int index = WorldPositionToFlatIndex(worldPosition);
        SetItem(item, index);
    }

    public void SetItem(T item, int index)
    {
        matrix[index] = item;
        isSet[index] = 1;
    }
    
    public bool TryGetItem(float3 worldPosition, out T item)
	{
        if(!WorldPositionIsInMatrix(worldPosition) || !ItemIsSet(worldPosition))
        {
            item = new T();
            return false;
        }

		item = GetItem(worldPosition);
        return true;
	}

    public T GetItem(float3 worldPosition)
    {
        int index = WorldPositionToFlatIndex(worldPosition);
		return GetItem(index);
    }

    public T GetItem(int index)
    {
        return matrix[index];
    }

    public void UnsetItem(float3 worldPosition)
    {
        UnsetItem(worldPosition);
    }

    public void UnsetItem(int index)
    {
        matrix[index] = new T();
        isSet[index] = 0;
    }

    public bool ItemIsSet(int2 worldPosition)
    {
        return ItemIsSet(new float3(worldPosition.x, 0, worldPosition.y));
    }

    public bool ItemIsSet(float3 worldPosition)
    {
        if(!WorldPositionIsInMatrix(worldPosition))
            return false;

        return ItemIsSet(WorldPositionToFlatIndex(worldPosition));
    }

    public bool ItemIsSet(int index)
    {
        if(index < 0 || index >= matrix.Length)
            return false;

        return isSet[index] > 0;
    }

    public float3 RepositionResize(float3 matrixPosition)
    {
        int x = (int)matrixPosition.x;
        int z = (int)matrixPosition.z;

        float3 rootPositionChange = float3.zero;
        float3 widthChange = float3.zero;

        if(x < 0)
        {
            int rightGap = EmptyLayersAtEdge(0);
            rootPositionChange.x = x;

            widthChange.x = (x * -1) - rightGap;
            if(widthChange.x < 0) widthChange.x = 0;
            
        }
        else if(x >= width)
        {
            int leftGap = EmptyLayersAtEdge(1);
            widthChange.x = x - (width - 1) - leftGap;
            
            rootPositionChange.x = leftGap;
        }

        if(z < 0)
        {
            int topGap = EmptyLayersAtEdge(2);
            rootPositionChange.z = z;

            widthChange.z = (z * -1) - topGap;
            if(widthChange.z < 0) widthChange.z = 0;
        }
        else if(z >= width)
        {
            int bottomGap = EmptyLayersAtEdge(3);
            widthChange.z = z - (width - 1) - bottomGap;

            rootPositionChange.z = bottomGap;
        }

        rootPositionChange -= 3;
        widthChange += 6;

        int newWidth = width;
        if(widthChange.x+widthChange.z > 0)
            newWidth += math.max((int)widthChange.x, (int)widthChange.z);

        float3 rootIndexOffset = rootPositionChange * -1;

        CopyToAdjustedMatrix(rootIndexOffset, newWidth);

        rootPosition += rootPositionChange * itemWorldSize;

        return rootIndexOffset;
    }

    int EmptyLayersAtEdge(int rightLeftUpDown)
    {
        int count = 0;

        while(LayerIsEmpty(rightLeftUpDown, count))
            count++;

        return count;
    }

    bool LayerIsEmpty(int rightLeftUpDown, int offset = 0)
    {
        if(offset >= math.floor(width/2)) return false;

        if(rightLeftUpDown < 2)
        {
            int x       = rightLeftUpDown == 0 ? width-1 : 0;
            int xOffset = rightLeftUpDown == 0 ? -offset : offset;
            for(int z  = 0; z < width; z++)
                if(ItemIsSet( PositionToIndex(new float3(x+xOffset, 0, z)) ))
                    return false;
        }
        else
        {
            int z       = rightLeftUpDown == 2 ? width-1 : 0;
            int zOffset = rightLeftUpDown == 2 ? -offset : offset;
            for(int x  = 0; x < width; x++)
                if(ItemIsSet( PositionToIndex(new float3(x, 0, z+zOffset)) ))
                    return false;
        }

        return true;
    }

    public void CopyToAdjustedMatrix(float3 rootIndexOffset, int newWidth)
    {
        NativeArray<T> newMatrix = new NativeArray<T>((int)math.pow(newWidth, 2), label);
        NativeArray<sbyte> newIsSet = new NativeArray<sbyte>(newMatrix.Length, label);

        for(int i = 0; i < matrix.Length; i++)
        {
            float3 oldMatrixPosition = IndexToPosition(i);
            float3 newMatrixPosition = oldMatrixPosition + rootIndexOffset;

            int newIndex = util.Flatten2D(newMatrixPosition, newWidth);
            if(newIndex < 0 || newIndex >= newMatrix.Length) continue;

            newMatrix[newIndex] = matrix[i];
            newIsSet[newIndex] = isSet[i];
        }

        width = newWidth;

        if(!job)   Dispose();

        matrix = newMatrix;
        isSet = newIsSet;
    }

    bool WorldPositionIsInMatrix(float3 worldPosition, int offset = 0)
	{
        float3 matrixPosition = WorldToMatrixPosition(worldPosition);

        return PositionIsInMatrix(matrixPosition, offset);
	}
    
    bool PositionIsInMatrix(float3 matrixPosition, int offset = 0)
	{
        int arrayWidth = width-1;

		if(	matrixPosition.x >= offset && matrixPosition.x <= arrayWidth-offset &&
			matrixPosition.z >= offset && matrixPosition.z <= arrayWidth-offset )
			return true;
		else
			return false;
	}

    bool InDistanceFromWorldPosition(float3 inDistanceFromWorld, float3 positionWorld, int offset)
    {
        float3 inDistanceFrom = WorldToMatrixPosition(inDistanceFromWorld);
        float3 position = WorldToMatrixPosition(positionWorld);
        return InDistancceFromPosition(inDistanceFrom, position, offset);
    }

    bool InDistancceFromPosition(float3 inDistanceFrom, float3 position, int offset)
    {
        if(	inDistanceFrom.x >= position.x - offset &&
            inDistanceFrom.z >= position.z - offset &&
			inDistanceFrom.x <= position.x + offset &&
            inDistanceFrom.z <= position.z + offset )
			return true;
		else
			return false;
    }

    int WorldPositionToFlatIndex(float3 worldPosition)
    {
        return PositionToIndex(WorldToMatrixPosition(worldPosition));
    }

    public float3 WorldToMatrixPosition(float3 worldPosition)
    {
        return (worldPosition - rootPosition) / itemWorldSize;
    }

    float3 FlatIndexToWorldPosition(int index)
    {
        return MatrixToWorldPosition(IndexToPosition(index));
    }

    public float3 MatrixToWorldPosition(float3 matrixPosition)
    {
        return (matrixPosition * itemWorldSize) + rootPosition;
    }

    public int PositionToIndex(float3 matrixPosition)
    {
        return util.Flatten2D(matrixPosition, width);
    }

    float3 IndexToPosition(int index)
    {
        return util.Unflatten2D(index, width);
    }
}
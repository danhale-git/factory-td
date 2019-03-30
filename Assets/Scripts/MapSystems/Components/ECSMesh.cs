using Unity.Entities;
using Unity.Mathematics;

namespace ECSMesh
{
    [InternalBufferCapacity(0)]
	public struct Vertex : IBufferElementData
	{
		public float3 vertex;
	}

	[InternalBufferCapacity(0)]
	public struct Normal : IBufferElementData
	{
		public float3 normal;
	}

    [InternalBufferCapacity(0)]
	public struct VertColor : IBufferElementData
	{
		public float4 color;
	}

	[InternalBufferCapacity(0)]
	public struct Triangle : IBufferElementData
	{
		public int triangle;
	}
}
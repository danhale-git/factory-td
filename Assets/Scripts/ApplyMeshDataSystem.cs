using UnityEngine;

using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;

public class ApplyMeshDataSystem : ComponentSystem
{
    EntityManager entityManager;

    int squareWidth;

    ComponentGroup applyMeshGroup;

	public static Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DefaultMaterial.mat");

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        squareWidth = TerrainSettings.mapSquareWidth;

        EntityArchetypeQuery applyMeshQuery = new EntityArchetypeQuery{
			All = new ComponentType[] { typeof(MeshDataSystem.MeshVertex) },
			None = new ComponentType[] { typeof(WorleyCellSystem.CellComplete) }
		};
        applyMeshGroup = GetComponentGroup(applyMeshQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
		NativeArray<ArchetypeChunk> chunks = applyMeshGroup.CreateArchetypeChunkArray(Allocator.TempJob);

		ArchetypeChunkEntityType entityType = GetArchetypeChunkEntityType();
        ArchetypeChunkBufferType<MeshDataSystem.MeshVertex> vertType = GetArchetypeChunkBufferType<MeshDataSystem.MeshVertex>(true);
       // ArchetypeChunkBufferType<MeshDataSystem.MeshNormal> normType = GetArchetypeChunkBufferType<MeshDataSystem.MeshNormal>(true);
        ArchetypeChunkBufferType<MeshDataSystem.MeshTriangle> triType = GetArchetypeChunkBufferType<MeshDataSystem.MeshTriangle>(true);
        //ArchetypeChunkBufferType<MeshDataSystem.MeshVertColor> colorType = GetArchetypeChunkBufferType<MeshDataSystem.MeshVertColor>(true);


		for(int c = 0; c < chunks.Length; c++)
		{
			ArchetypeChunk chunk = chunks[c];

			NativeArray<Entity> entities = chunk.GetNativeArray(entityType);

            BufferAccessor<MeshDataSystem.MeshVertex> vertBuffers = chunk.GetBufferAccessor<MeshDataSystem.MeshVertex>(vertType);
            //BufferAccessor<MeshDataSystem.MeshNormal> normBuffers = chunk.GetBufferAccessor<MeshDataSystem.MeshNormal>(normType);
            BufferAccessor<MeshDataSystem.MeshTriangle> triBuffers = chunk.GetBufferAccessor<MeshDataSystem.MeshTriangle>(triType);
            //BufferAccessor<MeshDataSystem.MeshVertColor> colorBuffers = chunk.GetBufferAccessor<MeshDataSystem.MeshVertColor>(colorType);
		    
            for(int e = 0; e < entities.Length; e++)
			{
				Entity entity = entities[e];

                Mesh mesh = MakeMesh(vertBuffers[e], /*normBuffers[e], */ triBuffers[e]/*, colorBuffers[e] */);
                SetMeshComponent(mesh, entity, commandBuffer);

				commandBuffer.RemoveComponent(entity, typeof(MeshDataSystem.MeshVertex));
				//commandBuffer.RemoveComponent(entity, typeof(MeshDataSystem.MeshNormal));
				commandBuffer.RemoveComponent(entity, typeof(MeshDataSystem.MeshTriangle));
				//commandBuffer.RemoveComponent(entity, typeof(MeshDataSystem.MeshVertColor));

				commandBuffer.RemoveComponent(entity, typeof(WorleyNoise.PointData));
				commandBuffer.RemoveComponent(entity, typeof(TopologySystem.Topology));

				commandBuffer.AddComponent(entity, new WorleyCellSystem.CellComplete());
            }
        }

		Debug.Log("playback");

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    Mesh MakeMesh(DynamicBuffer<MeshDataSystem.MeshVertex> vertices, /*DynamicBuffer<MeshDataSystem.MeshNormal> normals,  */DynamicBuffer<MeshDataSystem.MeshTriangle> triangles/*, DynamicBuffer<MeshDataSystem.MeshVertColor> colors */)
	{
		Vector3[] verticesArray = new Vector3[vertices.Length];
        int[] trianglesArray = new int[triangles.Length];
        Vector3[] normalsArray 	= new Vector3[vertices.Length];		
        Color[] colorsArray 	= new Color[vertices.Length];
		for(int i = 0; i < vertices.Length; i++)
		{

			verticesArray[i] 	= vertices[i].vertex;
			normalsArray[i] 	= float3.zero;
			//colorsArray[i] 		= new Color(colors[i].color.x, colors[i].color.y, colors[i].color.z, colors[i].color.w);
			colorsArray[i] 		= Color.white;


		}

		for(int i = 0; i < triangles.Length; i++)
		{
            trianglesArray[i]   = triangles[i].triangle;
		}

		Mesh mesh 		= new Mesh();
		mesh.vertices	= verticesArray;
		mesh.normals 	= normalsArray;
		mesh.colors 	= colorsArray;
		mesh.SetTriangles(trianglesArray, 0);

		mesh.RecalculateNormals();

		return mesh;
	}

    // Apply mesh to MapSquare entity
	void SetMeshComponent(Mesh mesh, Entity entity, EntityCommandBuffer commandBuffer)
	{
		RenderMesh renderer = new RenderMesh();
		renderer.mesh = mesh;
		renderer.material = material;

		commandBuffer.AddSharedComponent(entity, renderer);
	}
}
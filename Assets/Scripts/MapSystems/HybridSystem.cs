using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Rendering;
using Unity.Transforms;

using UnityEditor;
using UnityEngine.AI;
using System.Collections.Generic;

namespace Tags
{
    public struct HybridGameObjectCreated : IComponentData { }
}

public class HybridSystem : ComponentSystem
{
    EntityManager entityManager = World.Active.EntityManager;
    EntityQuery hybridQuery;

    GameObject sectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Sector.prefab");

    public struct GameObjectComponent : ISharedComponentData
    {
        public GameObject gameObject;
    }

    protected override void OnCreate()
    {
        hybridQuery = GetEntityQuery(
            new EntityQueryDesc{
                All = new ComponentType[] { typeof(RenderMesh), typeof(Tags.TerrainEntity) },
                None = new ComponentType[] { typeof(Tags.HybridGameObjectCreated) }
            }
        );
    }

    protected override void OnUpdate()
    {
        var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        var chunks = hybridQuery.CreateArchetypeChunkArray(Allocator.TempJob);

        var entityType = GetArchetypeChunkEntityType();
        var translationType = GetArchetypeChunkComponentType<Translation>(true);
        var matrixType = GetArchetypeChunkComponentType<CellSystem.MatrixComponent>(true);
        var localToWorldType = GetArchetypeChunkComponentType<LocalToWorld>(true);
        var meshType = GetArchetypeChunkSharedComponentType<RenderMesh>();

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];

            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<Translation> translations = chunk.GetNativeArray(translationType);
            NativeArray<CellSystem.MatrixComponent> matrixComponents = chunk.GetNativeArray(matrixType);
            NativeArray<LocalToWorld> localToWorldComponents = chunk.GetNativeArray(localToWorldType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity entity = entities[e];
                float3 position = translations[e].Value;
                CellSystem.MatrixComponent matrix = matrixComponents[e];
                Mesh mesh = chunk.GetSharedComponentData<RenderMesh>(meshType, entityManager).mesh;
                float4x4 localToWorld = localToWorldComponents[e].Value;

                GameObject sectorGameObject = GameObject.Instantiate(sectorPrefab, position, Quaternion.identity);

                CreateCollider(sectorGameObject, mesh);

                NavMeshSurface navMeshComponent = sectorGameObject.GetComponent<NavMeshSurface>();

                NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
                List<NavMeshBuildSource> sources = GetSourceList(matrix.width, navMeshComponent, mesh, localToWorld);
                Bounds bounds = CalculateWorldBounds(sources, position);
                
                var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, position, Quaternion.identity);

                if(data != null)
                {
                    data.name = sectorGameObject.name;
                    navMeshComponent.RemoveData();
                    navMeshComponent.navMeshData = data;
                    if(navMeshComponent.isActiveAndEnabled)
                        navMeshComponent.AddData();
                }

                commandBuffer.AddComponent<Tags.HybridGameObjectCreated>(entity, new Tags.HybridGameObjectCreated());
                commandBuffer.AddSharedComponent<GameObjectComponent>(entity, new GameObjectComponent { gameObject = sectorGameObject });
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    void CreateCollider(GameObject sectorGameObject, Mesh mesh)
    {
        MeshCollider collider = sectorGameObject.GetComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    List<NavMeshBuildSource> GetSourceList(int matrixWidth, NavMeshSurface component, Mesh mesh, float4x4 localToWorld)
    {
        NavMeshBuildSource source = new NavMeshBuildSource();
        source.shape = NavMeshBuildSourceShape.Mesh;
        source.size = new Vector3(matrixWidth, 0, matrixWidth);
        source.component = component;
        source.sourceObject = mesh;
        source.transform = localToWorld;

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        sources.Add(source);

        return sources;
    }

    Bounds CalculateWorldBounds(List<NavMeshBuildSource> sources, float3 position)
    {
        // Use the unscaled matrix for the NavMeshSurface
        Matrix4x4 worldToLocal = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
        worldToLocal = worldToLocal.inverse;

        var result = new Bounds();
        foreach (var src in sources)
        {
            switch (src.shape)
            {
                case NavMeshBuildSourceShape.Mesh:
                {
                    var m = src.sourceObject as Mesh;
                    result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, m.bounds));
                    break;
                }
                case NavMeshBuildSourceShape.Terrain:
                {
                    // Terrain pivot is lower/left corner - shift bounds accordingly
                    var t = src.sourceObject as TerrainData;
                    result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, new Bounds(0.5f * t.size, t.size)));
                    break;
                }
                case NavMeshBuildSourceShape.Box:
                case NavMeshBuildSourceShape.Sphere:
                case NavMeshBuildSourceShape.Capsule:
                case NavMeshBuildSourceShape.ModifierBox:
                    result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, new Bounds(Vector3.zero, src.size)));
                    break;
            }
        }
        // Inflate the bounds a bit to avoid clipping co-planar sources
        result.Expand(0.1f);
        return result;
    }
    static Bounds GetWorldBounds(Matrix4x4 mat, Bounds bounds)
    {
        var absAxisX = math.abs(mat.MultiplyVector(Vector3.right));
        var absAxisY = math.abs(mat.MultiplyVector(Vector3.up));
        var absAxisZ = math.abs(mat.MultiplyVector(Vector3.forward));
        var worldPosition = mat.MultiplyPoint(bounds.center);
        var worldSize = absAxisX * bounds.size.x + absAxisY * bounds.size.y + absAxisZ * bounds.size.z;
        return new Bounds(worldPosition, worldSize);
    }
}

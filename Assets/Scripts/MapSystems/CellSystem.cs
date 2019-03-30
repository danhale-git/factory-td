using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;
using Unity.Rendering;

using MapGeneration;

[AlwaysUpdateSystem]
public class CellSystem : ComponentSystem
{
    EntityManager entityManager;

    WorleyNoise worley;
    float cellValue;

    EntityArchetype cellArchetype;

    public struct CellComplete : IComponentData { }

    public struct CellMatrix : IComponentData
    {
        public float3 root;
        public int width;

        public T GetItem<T>(float3 worlPosition, DynamicBuffer<T> data, ArrayUtil util) where T : struct, IBufferElementData
        {
            int index = util.Flatten2D(worlPosition - root, width);
            return data[index];
        }

        public T GetItem<T>(int2 matrixPosition, DynamicBuffer<T> data, ArrayUtil util) where T : struct, IBufferElementData
        {
            int index = util.Flatten2D(matrixPosition.x, matrixPosition.y, width);
            return data[index];
        }
    }

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();

        worley = new WorleyNoise(
            TerrainSettings.seed,
            TerrainSettings.cellFrequency,
            TerrainSettings.cellEdgeSmoothing,
            TerrainSettings.cellularJitter
        );

        cellArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<LocalToWorld>(),
            ComponentType.ReadWrite<Translation>(),
            ComponentType.ReadWrite<RenderMeshProxy>()
        );

        for(int x = 0; x < 5; x++)
            for(int z = 0; z < 5; z++)
            {
                DiscoverCellJob(new int2(x, z));
            } 

        //DiscoverCellJob(int2.zero);
    }

    protected override void OnUpdate()
    {
        
    }

    void DiscoverCellJob(int2 index)
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        
        Entity cellEntity = entityManager.CreateEntity(cellArchetype);
        WorleyNoise.CellData cell = worley.GetCellData(index);
        entityManager.AddComponentData<WorleyNoise.CellData>(cellEntity, cell);

        DiscoverCellJob job = new DiscoverCellJob{
            commandBuffer = commandBuffer,
            cellEntity = cellEntity,
            matrix = new Matrix<WorleyNoise.PointData>(10, Allocator.TempJob, cell.position),
            worley = this.worley,
            cell = cell
        };
        job.Schedule().Complete();

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
}

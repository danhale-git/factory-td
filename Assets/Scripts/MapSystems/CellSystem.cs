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
    PlayerEntitySystem playerSystem;

    WorleyNoise worley;

    EntityArchetype cellArchetype;
    Matrix<Entity> cellMatrix;

    int2 currentCellIndex;
    int2 previousCellIndex;

    JobHandle runningJobHandle;
	EntityCommandBuffer runningCommandBuffer;

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
        playerSystem = World.Active.GetOrCreateManager<PlayerEntitySystem>();

        cellMatrix = new Matrix<Entity>(5, Allocator.Persistent, float3.zero);

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

        previousCellIndex = new int2(100); 
    }

    protected override void OnDestroyManager()
    {
        cellMatrix.Dispose();
        if(runningCommandBuffer.IsCreated) runningCommandBuffer.Dispose();
    }

    protected override void OnUpdate()
    {
        if(runningCommandBuffer.IsCreated)
        {
            if(!runningJobHandle.IsCompleted) return;
            else JobCompleteAndBufferPlayback();
        }

        if(!UpdateCurrentCellIndex()) return;
        else DiscoverSurroundingCells();
    }

    void JobCompleteAndBufferPlayback()
	{
		runningJobHandle.Complete();

		runningCommandBuffer.Playback(entityManager);
		runningCommandBuffer.Dispose();
	}

    bool UpdateCurrentCellIndex()
    {
        if(playerSystem.player == null) return false;

        float3 roundedPosition = math.round(playerSystem.player.transform.position);
        int2 index = worley.GetPointData(roundedPosition.x, roundedPosition.z).currentCellIndex;

        if(index.Equals(previousCellIndex)) return false;
        else
        {
            previousCellIndex = currentCellIndex;
            currentCellIndex = index;
            return true;
        }
    }

    void DiscoverSurroundingCells()
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        JobHandle allHandles		= new JobHandle();
		JobHandle previousHandle	= new JobHandle();

        for(int x = -2; x < 2; x++)
            for(int z = -2; z < 2; z++)
        //for(int x = 0; x < 1; x++)
        //    for(int z = 0; z < 1; z++)
            {
                int2 index = currentCellIndex + new int2(x, z);
                if(!cellMatrix.ItemIsSet(index))
                {
                    JobHandle newHandle = DiscoverCellJob(index, commandBuffer, previousHandle);
                    allHandles = JobHandle.CombineDependencies(newHandle, allHandles);
                    previousHandle = newHandle;
                } 
            } 

        runningCommandBuffer = commandBuffer;
        runningJobHandle = allHandles; 
    }

    JobHandle DiscoverCellJob(int2 index, EntityCommandBuffer commandBuffer, JobHandle previousHandle)
    { 
        DebugSystem.Count("Cells");

        Entity cellEntity = entityManager.CreateEntity(cellArchetype);
        WorleyNoise.CellData cell = worley.GetCellData(index);
        entityManager.AddComponentData<WorleyNoise.CellData>(cellEntity, cell);

        cellMatrix.AddItem(cellEntity, cell.index);

        DiscoverCellJob job = new DiscoverCellJob{
            commandBuffer = commandBuffer,
            cellEntity = cellEntity,
            matrix = new Matrix<WorleyNoise.PointData>(10, Allocator.TempJob, cell.position, job: true),
            worley = this.worley,
            cell = cell
        };
        return job.Schedule(previousHandle);
    }

    public float GetHeight(float3 position)
    {
        float3 roundedPosition = math.round(position);
        int2 cellIndex = worley.GetPointData(roundedPosition.x, roundedPosition.z).currentCellIndex;
        Entity cellEntity = cellMatrix.GetItem(new float3(cellIndex.x, 0, cellIndex.y));

        if(!entityManager.HasComponent<TopologySystem.Topology>(cellEntity))
            return 0;

        DynamicBuffer<TopologySystem.Topology> heightData = entityManager.GetBuffer<TopologySystem.Topology>(cellEntity);
        CellMatrix matrix = entityManager.GetComponentData<CellMatrix>(cellEntity);

        float height = matrix.GetItem(roundedPosition, heightData, new ArrayUtil()).height;

        return height;
    }
}

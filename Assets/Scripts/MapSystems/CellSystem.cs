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
    Biomes biomes;
    SimplexNoiseGenerator heightSimplex;
    SimplexNoiseGenerator groupSimplex;

    EntityArchetype cellArchetype;
    Matrix<WorleyNoise.CellData> cellMatrix;

    int2 currentCellIndex;
    int2 previousCellIndex;

    JobHandle runningJobHandle;
	EntityCommandBuffer runningCommandBuffer;

    NativeQueue<int2> floodFillQueue;
    NativeList<WorleyNoise.CellData> cellsInGroup;

    ArrayUtil arrayUtil;

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

        cellMatrix = new Matrix<WorleyNoise.CellData>(5, Allocator.Persistent, float3.zero);
        cellArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<LocalToWorld>(),
            ComponentType.ReadWrite<Translation>(),
            ComponentType.ReadWrite<RenderMeshProxy>()
        );

        biomes = new Biomes();
        worley = new WorleyNoise(
            TerrainSettings.seed,
            TerrainSettings.cellFrequency,
            TerrainSettings.cellEdgeSmoothing,
            TerrainSettings.cellularJitter,
            TerrainSettings.cellDistanceFunction,
            TerrainSettings.cellReturnType
        );
        heightSimplex = TerrainSettings.HeightSimplex();
        groupSimplex = TerrainSettings.GroupSimplex();
        
        arrayUtil = new ArrayUtil();

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
        else GenerateSurroundingCells();
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

    void GenerateSurroundingCells()
    {
        runningCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        runningJobHandle = new JobHandle();
		JobHandle previousHandle	= new JobHandle();

        for(int x = -2; x < 2; x++)
            for(int z = -2; z < 2; z++)
            {
                int2 index = currentCellIndex + new int2(x, z);
                if(!cellMatrix.ItemIsSet(index))
                {
                    floodFillQueue = new NativeQueue<int2>(Allocator.Temp);
                    cellsInGroup = new NativeList<WorleyNoise.CellData>(Allocator.Temp);

                    floodFillQueue.Enqueue(index);
                    while(floodFillQueue.Count > 0)
                    {
                        int2 cellIndex = floodFillQueue.Dequeue();
                        if(cellMatrix.ItemIsSet(cellIndex)) continue;
    
                        WorleyNoise.CellData cell = worley.GetCellData(cellIndex);
                        cellsInGroup.Add(cell);

                        cellMatrix.AddItem(cell, cellIndex);

                        EnqueueAdjacentInGroup(cellIndex);
                    }

                    NativeArray<WorleyNoise.CellData> sortedGroup = new NativeArray<WorleyNoise.CellData>(cellsInGroup, Allocator.Temp);
                    sortedGroup.Sort();

                    JobHandle newHandle = ScheduleCellJob(entityManager.CreateEntity(cellArchetype), sortedGroup[0].value, sortedGroup[0], previousHandle);
                    previousHandle = newHandle;


                    sortedGroup.Dispose();

                    cellsInGroup.Dispose();
                    floodFillQueue.Dispose();
                }
            } 
    }

    JobHandle ScheduleCellJob(Entity cellEntity, float group, WorleyNoise.CellData startCell, JobHandle previousHandle)
    { 
        FloodFillCellJob job = new FloodFillCellJob{
            commandBuffer = runningCommandBuffer,
            cellEntity = cellEntity,
            matrix = new Matrix<WorleyNoise.PointData>(10, Allocator.TempJob, startCell.position, job: true),
            worley = this.worley,
            startCell = startCell,
            group = biomes.CellGrouping(startCell.index, groupSimplex, heightSimplex),
            heightSimplex = heightSimplex,
            groupSimplex = groupSimplex
        };

        JobHandle newHandle = job.Schedule(previousHandle);
        runningJobHandle = JobHandle.CombineDependencies(newHandle, runningJobHandle);

        return newHandle;
    }

    void EnqueueAdjacentInGroup(int2 center)
    {
        float centerGrouping = biomes.CellGrouping(center, groupSimplex, heightSimplex);
        
        for(int x = -1; x <= 1; x++)
            for(int z = -1; z <= 1; z++)
            {
                int2 baseIndex = new int2(x, z);
                if(CornerOrCenter(baseIndex)) continue;

                int2 adjacent = center + baseIndex;
                float adjacentGrouping = biomes.CellGrouping(adjacent, groupSimplex, heightSimplex);

                if(adjacentGrouping == centerGrouping) floodFillQueue.Enqueue(adjacent);
            }
    }

    bool CornerOrCenter(int2 index)
    {
        return index.Equals(int2.zero) || !(index.x == 0 || index.y == 0);
    }

    public float GetHeightAtPosition(float3 position)
    {
        return 0;
        /*float3 roundedPosition = math.round(position);
        int2 cellIndex = worley.GetPointData(roundedPosition.x, roundedPosition.z).currentCellIndex;
        Entity cellEntity = cellMatrix.GetItem(cellIndex);

        if(!entityManager.HasComponent<TopologySystem.Height>(cellEntity))
            return 0;

        DynamicBuffer<TopologySystem.Height> heightData = entityManager.GetBuffer<TopologySystem.Height>(cellEntity);
        CellMatrix matrix = entityManager.GetComponentData<CellMatrix>(cellEntity);

        return matrix.GetItem(roundedPosition, heightData, new ArrayUtil()).height; */
    }
}

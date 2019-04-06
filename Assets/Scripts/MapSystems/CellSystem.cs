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

    EntityArchetype cellArchetype;
    Matrix<Entity> cellMatrix;

    int2 currentCellIndex;
    int2 previousCellIndex;

    JobHandle runningJobHandle;
	EntityCommandBuffer runningCommandBuffer;

    SimplexNoiseGenerator heightSimplex;
    SimplexNoiseGenerator groupSimplex;

    ArrayUtil arrayUtil;

    public struct CellComplete : IComponentData { }

    public struct Group : IComponentData { public float Value; }

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

        heightSimplex = TerrainSettings.HeightSimplex();
        groupSimplex = TerrainSettings.GroupSimplex();

        arrayUtil = new ArrayUtil();

        worley = new WorleyNoise(
            TerrainSettings.seed,
            TerrainSettings.cellFrequency,
            TerrainSettings.cellEdgeSmoothing,
            TerrainSettings.cellularJitter,
            TerrainSettings.cellDistanceFunction,
            TerrainSettings.cellReturnType
        );

        biomes = new Biomes();

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
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        JobHandle allHandles		= new JobHandle();
		JobHandle previousHandle	= new JobHandle();

        for(int x = -2; x < 2; x++)
            for(int z = -2; z < 2; z++)
            {
                int2 index = currentCellIndex + new int2(x, z);
                if(!cellMatrix.ItemIsSet(index))
                {
                    NativeQueue<int2> toCreate = new NativeQueue<int2>(Allocator.Temp);
                    NativeList<WorleyNoise.CellData> group = new NativeList<WorleyNoise.CellData>(Allocator.Temp);

                    toCreate.Enqueue(index);
                    while(toCreate.Count > 0)
                    {
                        int2 cellIndex = toCreate.Dequeue();
                        if(cellMatrix.ItemIsSet(cellIndex)) continue;
    
                        group.Add(worley.GetCellData(cellIndex));

                        JobHandle newHandle = ScheduleCellJob(cellIndex, commandBuffer, previousHandle);
                        allHandles = JobHandle.CombineDependencies(newHandle, allHandles);
                        previousHandle = newHandle;

                        CheckAdjacent(toCreate, cellIndex);
                    }

                    NativeArray<WorleyNoise.CellData> sortedGroup = new NativeArray<WorleyNoise.CellData>(group, Allocator.Temp);
                    sortedGroup.Sort();

                    float masterValue = sortedGroup[0].value;
                    for(int i = 0; i < sortedGroup.Length; i++)
                    {
                        Entity entity = cellMatrix.GetItem(sortedGroup[i].index);
                        entityManager.AddComponentData(entity, new Group { Value = masterValue } );
                    }

                    group.Dispose();
                    sortedGroup.Dispose();
                    toCreate.Dispose();
                }
            } 

        runningCommandBuffer = commandBuffer;
        runningJobHandle = allHandles; 
    }

    JobHandle ScheduleCellJob(int2 index, EntityCommandBuffer commandBuffer, JobHandle previousHandle)
    { 
        DebugSystem.Count("Cells");

        Entity cellEntity = entityManager.CreateEntity(cellArchetype);
        WorleyNoise.CellData cell = worley.GetCellData(index);
        entityManager.AddComponentData<WorleyNoise.CellData>(cellEntity, cell);

        cellMatrix.AddItem(cellEntity, cell.index);

        FloodFillCellJob job = new FloodFillCellJob{
            commandBuffer = commandBuffer,
            cellEntity = cellEntity,
            matrix = new Matrix<WorleyNoise.PointData>(10, Allocator.TempJob, cell.position, job: true),
            worley = this.worley,
            cell = cell
        };
        return job.Schedule(previousHandle);
    }

    void CheckAdjacent(NativeQueue<int2> toCreate, int2 center)
    {
        float centerGrouping = biomes.CellGrouping(center, groupSimplex, heightSimplex);
        
        for(int x = -1; x <= 1; x++)
            for(int z = -1; z <= 1; z++)
            {
                int2 baseIndex = new int2(x, z);
                if(baseIndex.Equals(int2.zero)) continue;
                if(!(x == 0 || z == 0)) continue;

                int2 adjacent = center + baseIndex;

                float adjacentGrouping = biomes.CellGrouping(adjacent, groupSimplex, heightSimplex);

                if(adjacentGrouping == centerGrouping) toCreate.Enqueue(adjacent);
            }
    }

    public float GetHeightAtPosition(float3 position)
    {
        float3 roundedPosition = math.round(position);
        int2 cellIndex = worley.GetPointData(roundedPosition.x, roundedPosition.z).currentCellIndex;
        Entity cellEntity = cellMatrix.GetItem(cellIndex);

        if(!entityManager.HasComponent<TopologySystem.Height>(cellEntity))
            return 0;

        DynamicBuffer<TopologySystem.Height> heightData = entityManager.GetBuffer<TopologySystem.Height>(cellEntity);
        CellMatrix matrix = entityManager.GetComponentData<CellMatrix>(cellEntity);

        return matrix.GetItem(roundedPosition, heightData, new ArrayUtil()).height;
    }
}

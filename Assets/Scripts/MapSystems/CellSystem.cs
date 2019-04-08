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
    Matrix<Entity> cellMatrix;

    int2 currentCellIndex;
    int2 previousCellIndex;

	EntityCommandBuffer runningCommandBuffer;
    JobHandle runningJobHandle;
    JobHandle previousHandle;

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

    /*public enum CellType { NONE, UNPATHABLE }

    public struct Type : IComponentData { public CellType Value; } */

    protected override void OnCreateManager()
    {
        entityManager = World.Active.GetOrCreateManager<EntityManager>();
        playerSystem = World.Active.GetOrCreateManager<PlayerEntitySystem>();

        cellMatrix = new Matrix<Entity>(5, Allocator.Persistent, float3.zero);
        cellArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<LocalToWorld>(),
            ComponentType.ReadWrite<Translation>(),
            ComponentType.ReadWrite<RenderMeshProxy>()

            //ComponentType.ReadWrite<Type>()
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
		previousHandle = new JobHandle();

        for(int x = -2; x < 2; x++)
            for(int z = -2; z < 2; z++)
            {
                int2 cellIndex = new int2(x, z) + currentCellIndex;
                
                if(cellMatrix.ItemIsSet(cellIndex)) continue;

                NativeList<WorleyNoise.CellData> group = FloodFillCellGroup(cellIndex);

                /*if(!GroupIsConnected(group))
                    for(int i = 0; i < group.Length; i++)
                        entityManager.SetComponentData(cellMatrix.GetItem(group[i].index), new Type { Value = CellType.UNPATHABLE }); */

                group.Dispose();
            }
    }

    /*bool GroupIsConnected(NativeList<WorleyNoise.CellData> group)
    {
        for(int i = 0; i < group.Length; i++)
        {
            int debug = group[i].index.Equals(new int2(-3,4)) ? 1 : 0;

            WorleyNoise.CellData data = group[i];
            Entity cellEntity = cellMatrix.GetItem(data.index);

            float grouping = biomes.CellGrouping(data.index, groupSimplex, heightSimplex);
            float height = biomes.CellHeight(data.index, heightSimplex);

            for(int x = -1; x <= 1; x++)
                for(int z = -1; z <= 1; z++)
                {
                    int2 direction = new int2(x, z);
                    if(direction.Equals(int2.zero)) continue;

                    int2 adjacentIndex = data.index + direction;

                    if(debug > 0)Debug.Log(direction);

                    WorleyNoise.CellData adjacentData = worley.GetCellData(adjacentIndex);

                    float adjacentGrouping = biomes.CellGrouping(adjacentIndex, groupSimplex, heightSimplex);
                    if(adjacentGrouping == grouping && !CornerOrCenter(direction))
                    {
                        if(debug > 0)Debug.Log("same group");
                        continue;
                    }

                    float adjacentHeight = biomes.CellHeight(adjacentIndex, heightSimplex);
                    bool sameHeight = height == adjacentHeight;

                    bool edgeSloped = biomes.EdgeIsSloped(direction, data.value, adjacentData.value, debug);

                    if(debug > 0)Debug.Log(sameHeight+" || "+edgeSloped);

                    if(sameHeight || edgeSloped) return true;
                }
        }

        return false;
    } */

    NativeList<WorleyNoise.CellData> FloodFillCellGroup(int2 startIndex)
    {
        floodFillQueue = new NativeQueue<int2>(Allocator.Temp);
        cellsInGroup = new NativeList<WorleyNoise.CellData>(Allocator.Temp);

        floodFillQueue.Enqueue(startIndex);
        while(floodFillQueue.Count > 0)
        {
            int2 cellIndex = floodFillQueue.Dequeue();
            if(cellMatrix.ItemIsSet(cellIndex)) continue;

            Entity cellEntity = CreateCell(cellIndex);
            ScheduleCellJob(cellEntity);

            EnqueueAdjacentInGroup(cellIndex);
        }

        floodFillQueue.Dispose();

        return cellsInGroup;
    }

    Entity CreateCell(int2 cellIndex)
    {
        cellsInGroup.Add(worley.GetCellData(cellIndex));

        Entity cellEntity = entityManager.CreateEntity(cellArchetype);
        entityManager.AddComponentData<WorleyNoise.CellData>(cellEntity, worley.GetCellData(cellIndex));

        return cellEntity;
    }

    void ScheduleCellJob(Entity cellEntity)
    { 
        WorleyNoise.CellData cell = entityManager.GetComponentData<WorleyNoise.CellData>(cellEntity);
        cellMatrix.AddItem(cellEntity, cell.index);

        FloodFillCellJob job = new FloodFillCellJob{
            commandBuffer = runningCommandBuffer,
            cellEntity = cellEntity,
            matrix = new Matrix<WorleyNoise.PointData>(10, Allocator.TempJob, cell.position, job: true),
            worley = this.worley,
            cell = cell
        };

        JobHandle newHandle = job.Schedule(previousHandle);
        runningJobHandle = JobHandle.CombineDependencies(newHandle, runningJobHandle);
        previousHandle = newHandle;
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

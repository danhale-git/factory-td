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
    TopologyUtil biomes;

    EntityArchetype cellArchetype;
    Matrix<Entity> cellMatrix;
    EntityArchetype sectorArchetype;

    int2 currentCellIndex;
    int2 previousCellIndex;

	EntityCommandBuffer runningCommandBuffer;
    JobHandle runningJobHandle;
    JobHandle previousHandle;

    NativeQueue<int2> floodFillQueue;
    NativeList<SectorSystem.Cell> cellsInGroup;

    ArrayUtil arrayUtil;

    public struct CellComplete : IComponentData { }

    public struct MatrixComponent : IComponentData
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
        entityManager = World.Active.GetOrCreateSystem<EntityManager>();
        playerSystem = World.Active.GetOrCreateSystem<PlayerEntitySystem>();

        cellMatrix = new Matrix<Entity>(5, Allocator.Persistent, float3.zero);
        cellArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<LocalToWorld>(),
            ComponentType.ReadWrite<Translation>(),
            ComponentType.ReadWrite<RenderMeshProxy>()//,

        );

        sectorArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<SectorSystem.Cell>()
        );

        biomes = new TopologyUtil();
        worley = new WorleyNoise(
            TerrainSettings.seed,
            TerrainSettings.cellFrequency,
            TerrainSettings.cellEdgeSmoothing,
            TerrainSettings.cellularJitter,
            TerrainSettings.cellDistanceFunction,
            TerrainSettings.cellReturnType
        );
        
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

                NativeList<SectorSystem.Cell> group = FloodFillCellGroup(cellIndex);

                CreateSectorEntity(group);
            }
    }

    NativeList<SectorSystem.Cell> FloodFillCellGroup(int2 startIndex)
    {
        floodFillQueue = new NativeQueue<int2>(Allocator.Temp);
        cellsInGroup = new NativeList<SectorSystem.Cell>(Allocator.Temp);

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

    void CreateSectorEntity(NativeList<SectorSystem.Cell> group)
    {
        Entity sectorEntity = entityManager.CreateEntity(sectorArchetype);
        entityManager.GetBuffer<SectorSystem.Cell>(sectorEntity).AddRange(group);
                        
        group.Dispose();
    }

    Entity CreateCell(int2 cellIndex)
    {
        Entity cellEntity = entityManager.CreateEntity(cellArchetype);
        entityManager.AddComponentData<WorleyNoise.CellData>(cellEntity, worley.GetCellData(cellIndex));

        cellsInGroup.Add(new SectorSystem.Cell{
                data = worley.GetCellData(cellIndex),
                entity = cellEntity
            }
        );

        DebugSystem.Count("Cell height "+biomes.CellHeight(cellIndex));

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
        float centerGrouping = biomes.CellGrouping(center);
        
        for(int x = -1; x <= 1; x++)
            for(int z = -1; z <= 1; z++)
            {
                int2 baseIndex = new int2(x, z);
                if(CornerOrCenter(baseIndex)) continue;

                int2 adjacent = center + baseIndex;
                float adjacentGrouping = biomes.CellGrouping(adjacent);

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
        MatrixComponent matrix = entityManager.GetComponentData<MatrixComponent>(cellEntity);

        return matrix.GetItem(roundedPosition, heightData, new ArrayUtil()).height;
    }

    public bool TryGetCell(int2 index, out Entity entity)
    {
        entity = new Entity();
        return cellMatrix.TryGetItem(index, out entity);
    }
}

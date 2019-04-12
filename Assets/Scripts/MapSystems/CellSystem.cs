using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

using Unity.Transforms;
using Unity.Rendering;

using MapGeneration;

namespace Tags
{
    public struct TerrainCell : IComponentData { }
}

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

    ArrayUtil arrayUtil;

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

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;
        playerSystem = World.Active.GetOrCreateSystem<PlayerEntitySystem>();

        cellMatrix = new Matrix<Entity>(5, Allocator.Persistent, float3.zero);
        cellArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<LocalToWorld>(),
            ComponentType.ReadWrite<Translation>(),
            ComponentType.ReadWrite<RenderMeshProxy>(),
            ComponentType.ReadWrite<Tags.TerrainCell>()
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

    protected override void OnDestroy()
    {
        cellMatrix.Dispose();
        if(runningCommandBuffer.IsCreated) runningCommandBuffer.Dispose();
    }
    
    /*protected override void OnStartRunning()
    {
        UpdateCurrentCellIndex();
        GenerateOneSector(currentCellIndex);
    } */

    protected override void OnUpdate()
    {
        UpdateCurrentCellIndex();

        if(runningCommandBuffer.IsCreated)
        {
            if(!runningJobHandle.IsCompleted) return;
            else JobCompleteAndBufferPlayback();
        }
        
        if(cellMatrix.ItemIsSet(currentCellIndex))
            GenerateAdjacentSectors();
        else
            GenerateOneSector(currentCellIndex);
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

    void GenerateOneSector(int2 cellIndex)
    {
        runningCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        runningJobHandle = new JobHandle();
		previousHandle = new JobHandle();

        CreateSector(cellIndex);
    }

    void GenerateAdjacentSectors()
    {
        runningCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        runningJobHandle = new JobHandle();
		previousHandle = new JobHandle();

        if(cellMatrix.ItemIsSet(currentCellIndex))
        {
            Entity sectorEntity = cellMatrix.GetItem(currentCellIndex);
            if(!entityManager.HasComponent<SectorSystem.AdjacentCell>(sectorEntity))
                return;

            NativeArray<SectorSystem.AdjacentCell> adjacentCells = AdjacentCells(sectorEntity);
            
            for(int i = 0; i < adjacentCells.Length; i++)
                CreateSector(adjacentCells[i].data.index);

            adjacentCells.Dispose();
        }
    }

    NativeArray<SectorSystem.AdjacentCell> AdjacentCells(Entity sectorEntity)
    {
        DynamicBuffer<SectorSystem.AdjacentCell> adjacentCells = entityManager.GetBuffer<SectorSystem.AdjacentCell>(sectorEntity);
        return new NativeArray<SectorSystem.AdjacentCell>(adjacentCells.AsNativeArray(), Allocator.Temp);
    }

    void CreateSector(int2 startIndex)
    {
        if(cellMatrix.ItemIsSet(startIndex)) return;
        Entity sectorEntity = CreateSectorEntity(startIndex);
        ScheduleCellGroupJob(sectorEntity);
    }

    Entity CreateSectorEntity(int2 cellIndex)
    {
        Entity sectorEntity = entityManager.CreateEntity(cellArchetype);
        entityManager.AddComponentData<WorleyNoise.CellData>(sectorEntity, worley.GetCellData(cellIndex));
        TrySetCell(sectorEntity, cellIndex);

        return sectorEntity;
    }

    void ScheduleCellGroupJob(Entity cellEntity)
    { 
        WorleyNoise.CellData startCell = entityManager.GetComponentData<WorleyNoise.CellData>(cellEntity);

        FloodFillCellGroupJob job = new FloodFillCellGroupJob{
            commandBuffer = runningCommandBuffer,
            startCell = startCell,
            sectorEntity = cellEntity,
            matrix = new Matrix<WorleyNoise.PointData>(10, Allocator.TempJob, startCell.position, job: true),
            worley = this.worley
        };

        JobHandle newHandle = job.Schedule(previousHandle);
        runningJobHandle = JobHandle.CombineDependencies(newHandle, runningJobHandle);
        previousHandle = newHandle;
    }

    public float GetHeightAtPosition(float3 position)
    {
        /*float3 roundedPosition = math.round(position);
        int2 cellIndex = worley.GetPointData(roundedPosition.x, roundedPosition.z).currentCellIndex;
        Entity cellEntity = cellMatrix.GetItem(cellIndex);

        if(!entityManager.HasComponent<TopologySystem.Height>(cellEntity))
            return 0;

        DynamicBuffer<TopologySystem.Height> heightData = entityManager.GetBuffer<TopologySystem.Height>(cellEntity);
        MatrixComponent matrix = entityManager.GetComponentData<MatrixComponent>(cellEntity);

        return matrix.GetItem(roundedPosition, heightData, new ArrayUtil()).height; */
        return 20;
    }

    public bool TryGetCell(int2 index, out Entity entity)
    {
        entity = new Entity();
        return cellMatrix.TryGetItem(index, out entity);
    }

    public bool TrySetCell(Entity sectorEntity, int2 index)
    {
        if(cellMatrix.ItemIsSet(index)) return false;

        cellMatrix.AddItem(sectorEntity, index);
        return true;
    }
}

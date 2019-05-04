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
    public struct TerrainEntity : IComponentData { }
}

[AlwaysUpdateSystem]
public class CellSystem : ComponentSystem
{
    EntityManager entityManager;
    PlayerEntitySystem playerSystem;

    EntityQuery sectorSortQuery;

    WorleyNoise worley;

    EntityArchetype sectorArchetype;
    Matrix<Entity> sectorMatrix;
    Matrix<CellMatrixItem> cellMatrix;

    public int2 currentCellIndex;
    int2 previousCellIndex;

    ASyncJobManager jobManager;

    ArrayUtil arrayUtil;
    TopologyUtil topologyUtil;

    public struct MatrixComponent : IComponentData
    {
        public float3 root;
        public int width;

        public T GetItem<T>(float3 worlPosition, DynamicBuffer<T> data, ArrayUtil util) where T : struct, IBufferElementData
        {
            int index = util.Flatten2D(worlPosition - root, width);
            return data[index];
        }

        public T GetItem<T>(int2 matrixPosition, NativeArray<T> data, ArrayUtil util) where T : struct, IBufferElementData
        {
            int index = util.Flatten2D(matrixPosition.x, matrixPosition.y, width);
            return data[index];
        }

        public T GetItem<T>(int2 matrixPosition, DynamicBuffer<T> data, ArrayUtil util) where T : struct, IBufferElementData
        {
            int index = util.Flatten2D(matrixPosition.x, matrixPosition.y, width);
            return data[index];
        }
    }

    [InternalBufferCapacity(0)]
    public struct CellSet : IBufferElementData
    {
        public WorleyNoise.PointData data;
    }

    [InternalBufferCapacity(0)]
    public struct AdjacentCell : IBufferElementData
    {
        public int2 index;
    }

    [InternalBufferCapacity(0)]
    public struct SectorCell : IBufferElementData
    {
        public int2 index;
    }

    public struct CellMatrixItem
    {
        public CellMatrixItem(WorleyNoise.CellData data, float grouping, float height)
        {
            this.data = data;
            this.grouping = grouping;
            this.height = height;
        }
        public readonly WorleyNoise.CellData data;
        public readonly float grouping;
        public readonly float height;
    }

    protected override void OnCreate()
    {
        entityManager = World.Active.EntityManager;
        playerSystem = World.Active.GetOrCreateSystem<PlayerEntitySystem>();

        UpdateCurrentCellIndex();
        previousCellIndex = new int2(100);

        sectorArchetype = entityManager.CreateArchetype(
            ComponentType.ReadWrite<LocalToWorld>(),
            ComponentType.ReadWrite<Translation>(),
            ComponentType.ReadWrite<RenderMeshProxy>()
        );

        float3 matrixRoot = new float3(currentCellIndex.x, 0, currentCellIndex.y);
        sectorMatrix = new Matrix<Entity>(5, Allocator.Persistent, matrixRoot);
        cellMatrix = new Matrix<CellMatrixItem>(5, Allocator.Persistent, matrixRoot);

        EntityQueryDesc sectorSortQueryDesc = new EntityQueryDesc{
            All = new ComponentType[] { typeof(AdjacentCell), typeof(SectorCell), typeof(WorleyNoise.PointData) },
            None = new ComponentType[] { typeof(Tags.TerrainEntity) }
        };
        sectorSortQuery = GetEntityQuery(sectorSortQueryDesc);

        worley = TerrainSettings.CellWorley();
        topologyUtil = new TopologyUtil().Construct();
    }

    protected override void OnDestroy()
    {
        sectorMatrix.Dispose();
        cellMatrix.Dispose();
        jobManager.Dispose();
    }

    protected override void OnUpdate()
    {
        UpdateCurrentCellIndex();

        if(!jobManager.AllJobsCompleted()) return;
        
        AddNewSectorsToMatrix();
        
        if(sectorMatrix.ItemIsSet(currentCellIndex))
            ScheduleFloodFillJobsForAdjacentGroups();
        else
            ScheduleFloodFillJobForCellGroup(currentCellIndex);
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

    void AddNewSectorsToMatrix()
    {
        var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        var chunks = sectorSortQuery.CreateArchetypeChunkArray(Allocator.TempJob);

        var entityType = GetArchetypeChunkEntityType();
        var cellArrayType = GetArchetypeChunkBufferType<SectorCell>(true);
        var adjacentCellArrayType = GetArchetypeChunkBufferType<AdjacentCell>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            var chunk = chunks[c];
            
            var entities = chunk.GetNativeArray(entityType);
            var cellArrays = chunk.GetBufferAccessor(cellArrayType);
            var adjacentCellArrays = chunk.GetBufferAccessor(adjacentCellArrayType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity sectorEntity = entities[e];
                DynamicBuffer<SectorCell> cells = cellArrays[e];
                DynamicBuffer<AdjacentCell> adjacentCells = adjacentCellArrays[e];

                for(int i = 0; i < cells.Length; i++)
                {
                    TryAddCell(cells[i].index);

                    TryAddSector(sectorEntity, cells[i].index);
                }

                for(int i = 0; i < adjacentCells.Length; i++)
                    TryAddCell(adjacentCells[i].index);

                commandBuffer.AddComponent<Tags.TerrainEntity>(sectorEntity, new Tags.TerrainEntity());
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    void ScheduleFloodFillJobsForAdjacentGroups()
    {
        Entity sectorEntity = sectorMatrix.GetItem(currentCellIndex);
        if(!entityManager.HasComponent<AdjacentCell>(sectorEntity))
            return;

        NativeArray<AdjacentCell> adjacentCells = GetAdjacentCellArray(sectorEntity);
        NativeList<float> alreadyCreatedCellGroups = new NativeList<float>(Allocator.Temp);
        
        for(int i = 0; i < adjacentCells.Length; i++)
        {
            int2 cellIndex = adjacentCells[i].index;
            float grouping = topologyUtil.CellGrouping(cellIndex);

            if(alreadyCreatedCellGroups.Contains(grouping))
                continue;

            bool groupWasCreated = ScheduleFloodFillJobForCellGroup(cellIndex);
            
            if(groupWasCreated)
                alreadyCreatedCellGroups.Add(grouping);
        }
        
        adjacentCells.Dispose();
        alreadyCreatedCellGroups.Dispose();
    }

    NativeArray<AdjacentCell> GetAdjacentCellArray(Entity sectorEntity)
    {
        DynamicBuffer<AdjacentCell> adjacentCells = entityManager.GetBuffer<AdjacentCell>(sectorEntity);
        return new NativeArray<AdjacentCell>(adjacentCells.AsNativeArray(), Allocator.Temp);
    }

    bool ScheduleFloodFillJobForCellGroup(int2 startIndex)
    {
        if(sectorMatrix.ItemIsSet(startIndex)) return false;
        Entity sectorEntity = CreateSectorEntity(startIndex);
        ScheduleCellGroupJob(sectorEntity);
        return true;
    }

    Entity CreateSectorEntity(int2 cellIndex)
    {
        Entity sectorEntity = entityManager.CreateEntity(sectorArchetype);
        entityManager.AddComponentData<WorleyNoise.CellData>(sectorEntity, worley.GetCellData(cellIndex));
        TryAddSector(sectorEntity, cellIndex);

        return sectorEntity;
    }

    void ScheduleCellGroupJob(Entity cellEntity)
    { 
        WorleyNoise.CellData startCell = entityManager.GetComponentData<WorleyNoise.CellData>(cellEntity);

        FloodFillCellGroupJob job = new FloodFillCellGroupJob{
            commandBuffer = jobManager.commandBuffer,
            startCell = startCell,
            sectorEntity = cellEntity,
            pointMatrix = new Matrix<WorleyNoise.PointData>(10, Allocator.TempJob, startCell.position, job: true),
            cellMatrix =  new Matrix<CellMatrixItem>(1, Allocator.TempJob, new float3(startCell.index.x, 0, startCell.index.y), job: true),
            worley = this.worley,
            topologyUtil = new TopologyUtil().Construct()
        };

        jobManager.ScheduleNewJob(job);
    }

    public float GetHeightAtPosition(float3 position)
    {
        float3 roundedPosition = math.round(position);
        int2 cellIndex = worley.GetPointData(roundedPosition.x, roundedPosition.z).currentCellIndex;
        Entity cellEntity;

        if(!sectorMatrix.TryGetItem(cellIndex, out cellEntity) || !entityManager.HasComponent<TopologySystem.Height>(cellEntity))
            return 0;

        DynamicBuffer<TopologySystem.Height> heightData = entityManager.GetBuffer<TopologySystem.Height>(cellEntity);
        MatrixComponent matrix = entityManager.GetComponentData<MatrixComponent>(cellEntity);

        return matrix.GetItem(roundedPosition, heightData, arrayUtil).height;
    }

    void TryAddCell(int2 index)
    {
        if(!cellMatrix.ItemIsSet(index))
        {
            WorleyNoise.CellData cellData = worley.GetCellData(index);
            CellMatrixItem cell = new CellMatrixItem
            (
                cellData,
                topologyUtil.CellGrouping(index),
                topologyUtil.CellHeight(index)
            );

            cellMatrix.AddItem(cell, cellData.index);
        }
    }

    public WorleyNoise.CellData GetCellData(int2 index)
    {
        return cellMatrix.GetItem(index).data;
    }

    public float GetCellHeight(int2 index)
    {
        return cellMatrix.GetItem(index).height;
    }
    
    public float GetCellGrouping(int2 index)
    {
        return cellMatrix.GetItem(index).grouping;
    }

    public bool TryGetSector(int2 index, out Entity entity)
    {
        entity = new Entity();
        return sectorMatrix.TryGetItem(index, out entity);
    }

    public bool TryAddSector(Entity sectorEntity, int2 index)
    {
        if(sectorMatrix.ItemIsSet(index)) return false;

        sectorMatrix.AddItem(sectorEntity, index);
        return true;
    }
}

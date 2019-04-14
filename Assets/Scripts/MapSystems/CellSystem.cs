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
    TopologyUtil biomes;

    EntityArchetype cellArchetype;
    Matrix<Entity> cellMatrix;
    EntityArchetype sectorArchetype;

    int2 currentCellIndex;
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
        public WorleyNoise.CellData data;
    }

    [InternalBufferCapacity(0)]
    public struct SectorCell : IBufferElementData
    {
        public WorleyNoise.CellData data;
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
            ComponentType.ReadWrite<Tags.TerrainEntity>()
        );

        EntityQueryDesc sectorSortQueryDesc = new EntityQueryDesc{
            All = new ComponentType[] { typeof(Tags.TerrainEntity), typeof(CellSet) },
            None = new ComponentType[] { typeof(AdjacentCell), typeof(SectorCell) }
        };
        sectorSortQuery = GetEntityQuery(sectorSortQueryDesc);

        biomes = new TopologyUtil();
        worley = TerrainSettings.CellWorley();
        
        previousCellIndex = new int2(100); 
    }

    protected override void OnDestroy()
    {
        cellMatrix.Dispose();
        jobManager.Dispose();
    }

    protected override void OnUpdate()
    {
        UpdateCurrentCellIndex();

        if(!jobManager.AllJobsCompleted()) return;
        
        ProcessNewTerrainCells();
        
        if(cellMatrix.ItemIsSet(currentCellIndex))
            GenerateAdjacentSectors();
        else
            CreateSector(currentCellIndex);
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

    void ProcessNewTerrainCells()
    {
        var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        var chunks = sectorSortQuery.CreateArchetypeChunkArray(Allocator.TempJob);

        var entityType = GetArchetypeChunkEntityType();
        var startCellType = GetArchetypeChunkComponentType<WorleyNoise.CellData>(true);
        var cellArrayType = GetArchetypeChunkBufferType<CellSet>(true);

        for(int c = 0; c < chunks.Length; c++)
        {
            ArchetypeChunk chunk = chunks[c];
            NativeArray<Entity> entities = chunk.GetNativeArray(entityType);
            NativeArray<WorleyNoise.CellData> startCells = chunk.GetNativeArray(startCellType);
            BufferAccessor<CellSet> cellArrays = chunk.GetBufferAccessor(cellArrayType);

            for(int e = 0; e < entities.Length; e++)
            {
                Entity sectorEntity = entities[e];
                DynamicBuffer<CellSet> cellSet = cellArrays[e];

                DynamicBuffer<SectorCell> sectorCells = commandBuffer.AddBuffer<SectorCell>(sectorEntity);
                DynamicBuffer<AdjacentCell> adjacentCells = commandBuffer.AddBuffer<AdjacentCell>(sectorEntity);

                float grouping = topologyUtil.CellGrouping(startCells[e].index);
                for(int i = 0; i < cellSet.Length; i++)
                {
                    WorleyNoise.CellData cellData = worley.GetCellData(cellSet[i].data.currentCellIndex);

                    if(cellData.value == 0) continue;

                    if(topologyUtil.CellGrouping(cellSet[i].data.currentCellIndex) != grouping)
                    {
                        adjacentCells.Add(new AdjacentCell{ data = cellData });
                    }
                    else
                    {
                        sectorCells.Add(new SectorCell{ data = cellData });
                        TrySetCell(sectorEntity, cellSet[i].data.currentCellIndex);
                    }
                }
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        chunks.Dispose();
    }

    void GenerateAdjacentSectors()
    {
        if(cellMatrix.ItemIsSet(currentCellIndex))
        {
            Entity sectorEntity = cellMatrix.GetItem(currentCellIndex);
            if(!entityManager.HasComponent<AdjacentCell>(sectorEntity))
                return;

            NativeArray<AdjacentCell> adjacentCells = AdjacentCells(sectorEntity);
            
            for(int i = 0; i < adjacentCells.Length; i++)
                if(CreateSector(adjacentCells[i].data.index))
                {
                    adjacentCells.Dispose();
                    return;
                }
        }
    }

    NativeArray<AdjacentCell> AdjacentCells(Entity sectorEntity)
    {
        DynamicBuffer<AdjacentCell> adjacentCells = entityManager.GetBuffer<AdjacentCell>(sectorEntity);
        return new NativeArray<AdjacentCell>(adjacentCells.AsNativeArray(), Allocator.Temp);
    }

    bool CreateSector(int2 startIndex)
    {
        if(cellMatrix.ItemIsSet(startIndex)) return false;
        Entity sectorEntity = CreateSectorEntity(startIndex);
        ScheduleCellGroupJob(sectorEntity);
        return true;
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
            commandBuffer = jobManager.commandBuffer,
            startCell = startCell,
            sectorEntity = cellEntity,
            matrix = new Matrix<WorleyNoise.PointData>(10, Allocator.TempJob, startCell.position, job: true),
            worley = this.worley
        };

        jobManager.ScheduleNewJob(job);
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

    public bool TrySetCell(Entity sectorEntity, int2 index)
    {
        if(cellMatrix.ItemIsSet(index)) return false;

        cellMatrix.AddItem(sectorEntity, index);
        return true;
    }
}

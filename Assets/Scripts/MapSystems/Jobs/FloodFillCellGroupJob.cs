using Unity.Jobs;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Burst;

namespace MapGeneration
{
    public struct FloodFillCellGroupJob : IJob
    {
        public EntityCommandBuffer commandBuffer;

        public Matrix<WorleyNoise.PointData> pointMatrix;
        public Matrix<CellSystem.CellMatrixItem> cellMatrix;

        [ReadOnly] public WorleyNoise.CellData startCell;

        [ReadOnly] public Entity sectorEntity;

        [ReadOnly] public WorleyNoise worley;
        [ReadOnly] public TopologyUtil topologyUtil;


        public void Execute()
        {
            FloodFillCell();

            AddSortedCellArrays();
            
            AddPointArrayFromMatrix();

            float3 matrixRootPosition = AddCellMatrixComponent();

            SetPosition(matrixRootPosition);
        }

        public void FloodFillCell()
        {
            NativeQueue<WorleyNoise.PointData> dataToCheck = new NativeQueue<WorleyNoise.PointData>(Allocator.Temp);

            WorleyNoise.PointData initialPointData = GetPointData(startCell.position);
            dataToCheck.Enqueue(initialPointData);

            float startCellGrouping = GetOrGenerateCellGrouping(startCell.index);
            initialPointData.cellGrouping = startCellGrouping;

            pointMatrix.AddItem(initialPointData, initialPointData.pointWorldPosition);

            while(dataToCheck.Count > 0)
            {
                DebugSystem.Count("Points flood filled");
                WorleyNoise.PointData data = dataToCheck.Dequeue();

                bool currentIsOutsideCell = GetOrGenerateCellGrouping(data.currentCellIndex) != startCellGrouping;

                for(int x = -1; x <= 1; x++)
                    for(int z = -1; z <= 1; z++)
                    {
                        float3 adjacentPosition = new float3(x, 0, z) + data.pointWorldPosition;
                        WorleyNoise.PointData adjacentData = GetPointData(adjacentPosition);

                        float grouping = GetOrGenerateCellGrouping(adjacentData.currentCellIndex);

                        bool adjacentIsOutsideCell = grouping != startCellGrouping;
                        if(pointMatrix.ItemIsSet(adjacentPosition) || (currentIsOutsideCell && adjacentIsOutsideCell))
                            continue;

                        adjacentData.cellGrouping = grouping;

                        dataToCheck.Enqueue(adjacentData);
                        pointMatrix.AddItem(adjacentData, adjacentData.pointWorldPosition);
                    }

            }

            dataToCheck.Dispose();
        }

        void AddSortedCellArrays()
        {
            float startCellGrouping = GetOrGenerateCellGrouping(startCell.index);

            DynamicBuffer<CellSystem.SectorCell> sectorCells = commandBuffer.AddBuffer<CellSystem.SectorCell>(sectorEntity);
            DynamicBuffer<CellSystem.AdjacentCell> adjacentCells = commandBuffer.AddBuffer<CellSystem.AdjacentCell>(sectorEntity);

            for(int i = 0; i < cellMatrix.Length; i++)
            {
                WorleyNoise.CellData cellData = cellMatrix.GetItem(i).data;

                if(cellData.value == 0)
                    continue;

                if(GetOrGenerateCellGrouping(cellData.index) != startCellGrouping)
                    adjacentCells.Add(new CellSystem.AdjacentCell{ index = cellData.index });
                else
                    sectorCells.Add(new CellSystem.SectorCell{ index = cellData.index });
            }
        }

        float GetOrGenerateCellGrouping(int2 index)
        {
            if(cellMatrix.ItemIsSet(index))
                return cellMatrix.GetItem(index).grouping;

            var cell = new CellSystem.CellMatrixItem
            (
                worley.GetCellData(index),
                topologyUtil.CellGrouping(index),
                topologyUtil.CellHeight(index)
            );
            
            cellMatrix.AddItem(cell, index);
            return cell.grouping;
        }

        WorleyNoise.PointData GetPointData(float3 position)
        {
            WorleyNoise.PointData data = worley.GetPointData(position.x, position.z);
            data.pointWorldPosition = position;
            data.isSet = true;
            return data;
        }

        void AddPointArrayFromMatrix()
        {
            DynamicBuffer<WorleyNoise.PointData> worleyBuffer = commandBuffer.AddBuffer<WorleyNoise.PointData>(sectorEntity);
            worleyBuffer.CopyFrom(pointMatrix.matrix);
        }

        float3 AddCellMatrixComponent()
        {
            CellSystem.MatrixComponent cellMatrix = new CellSystem.MatrixComponent{
                root = pointMatrix.rootPosition,
                width = pointMatrix.width
            };
            commandBuffer.AddComponent<CellSystem.MatrixComponent>(sectorEntity, cellMatrix);
            return cellMatrix.root;
        }

        void SetPosition(float3 position)
        {
            float3 pos = new float3(position.x, 0, position.z);
            commandBuffer.SetComponent(sectorEntity, new Translation{ Value = pos });
        }
    }
}
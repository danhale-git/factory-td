using Unity.Jobs;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;

namespace MapGeneration
{
    public struct DiscoverCellJob : IJob
    {
        public EntityCommandBuffer commandBuffer;

        public Entity cellEntity;

        public Matrix<WorleyNoise.PointData> matrix;
        public WorleyNoise worley;
        public WorleyNoise.CellData cell;

        public void Execute()
        {
            PopulateMatrix();
            
            AddBufferFromMatrix();

            CellSystem.CellMatrix cellMatrix = AddCellMatrixComponent();

            SetPosition(cellMatrix.root);
        }

        void PopulateMatrix()
        {
            NativeQueue<WorleyNoise.PointData> dataToCheck = new NativeQueue<WorleyNoise.PointData>(Allocator.Temp);

            WorleyNoise.PointData initialPointData = GetPointData(cell.position);
            dataToCheck.Enqueue(initialPointData);
            matrix.AddItem(initialPointData, initialPointData.pointWorldPosition);

            while(dataToCheck.Count > 0)
            {
                WorleyNoise.PointData data = dataToCheck.Dequeue();

                bool currentPositionInCell = data.currentCellValue == cell.value;

                for(int x = -1; x <= 1; x++)
                    for(int z = -1; z <= 1; z++)
                    {
                        float3 adjacentPosition = new float3(x, 0, z) + data.pointWorldPosition;
                        WorleyNoise.PointData adjacentData = GetPointData(adjacentPosition);

                        bool adjacentPositionInCell = adjacentData.currentCellValue == cell.value;
                        if(matrix.ItemIsSet(adjacentPosition) || (!currentPositionInCell && !adjacentPositionInCell)) continue;

                        dataToCheck.Enqueue(adjacentData);
                        matrix.AddItem(adjacentData, adjacentData.pointWorldPosition);
                    }

            }

            dataToCheck.Dispose();
        }

        WorleyNoise.PointData GetPointData(float3 position)
        {
            WorleyNoise.PointData data = worley.GetPointData(position.x, position.z);
            data.pointWorldPosition = position;
            data.isSet = 1;
            return data;
        }

        void AddBufferFromMatrix()
        {
            DynamicBuffer<WorleyNoise.PointData> worleyBuffer = commandBuffer.AddBuffer<WorleyNoise.PointData>(cellEntity);
            worleyBuffer.CopyFrom(matrix.matrix);
        }

        CellSystem.CellMatrix AddCellMatrixComponent()
        {
            CellSystem.CellMatrix cellMatrix = new CellSystem.CellMatrix{
                root = matrix.rootPosition,
                width = matrix.width
            };
            commandBuffer.AddComponent<CellSystem.CellMatrix>(cellEntity, cellMatrix);
            return cellMatrix;
        }

        void SetPosition(float3 position)
        {
            float3 pos = new float3(position.x, 0, position.z);
            commandBuffer.SetComponent(cellEntity, new Translation{ Value = pos });
        }
    }
}
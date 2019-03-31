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
            WorleyNoise.PointData initialPointData = GetPointData(cell.position);

            NativeQueue<float3> positionsToCheck = new NativeQueue<float3>(Allocator.Temp);
            NativeQueue<WorleyNoise.PointData> dataToCheck = new NativeQueue<WorleyNoise.PointData>(Allocator.Temp);

            positionsToCheck.Enqueue(cell.position);
            dataToCheck.Enqueue(initialPointData);
            matrix.AddItem(initialPointData, cell.position);

            while(positionsToCheck.Count > 0)
            {
                float3 position = positionsToCheck.Dequeue();
                WorleyNoise.PointData data = dataToCheck.Dequeue();

                bool currentInCell = data.currentCellValue == cell.value;

                for(int x = -1; x <= 1; x++)
                    for(int z = -1; z <= 1; z++)
                    {
                        float3 adjacentPosition = new float3(x, 0, z) + position;
                        WorleyNoise.PointData adjacentData = GetPointData(adjacentPosition);

                        bool adjacentInCell = adjacentData.currentCellValue == cell.value;
                        if(matrix.ItemIsSet(adjacentPosition) || (!currentInCell && !adjacentInCell)) continue;

                        positionsToCheck.Enqueue(adjacentPosition);
                        dataToCheck.Enqueue(adjacentData);
                        matrix.AddItem(adjacentData, adjacentPosition);
                    }

            }

            positionsToCheck.Dispose();
            dataToCheck.Dispose();
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

        /*void DiscoverPointsRecursively(float3 position, WorleyNoise.PointData data)
        {
            DebugSystem.Count("Discovery recursion");

            matrix.AddItem(data, position);

            bool currentInCell = data.currentCellValue == cell.value;

            for(int x = -1; x <= 1; x++)
                for(int z = -1; z <= 1; z++)
                {
                    float3 adjacent = new float3(x, 0, z) + position;

                    WorleyNoise.PointData adjacentData = GetPointData(adjacent);

                    bool adjacentInCell = adjacentData.currentCellValue == cell.value;

                    if(matrix.ItemIsSet(adjacent) || (!currentInCell && !adjacentInCell)) continue;


                    DiscoverPointsRecursively(adjacent, adjacentData);
                }
        } */

        WorleyNoise.PointData GetPointData(float3 position)
        {
            WorleyNoise.PointData data = worley.GetPointData(position.x, position.z);
            data.pointWorldPosition = position;
            data.isSet = 1;
            return data;
        }
    }
}
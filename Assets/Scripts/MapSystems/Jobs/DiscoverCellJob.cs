using Unity.Jobs;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;

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

            WorleyCellSystem.CellMatrix cellMatrix = AddCellMatrixComponent();

            SetPosition(cellMatrix.root);
        }

        void PopulateMatrix()
        {
            WorleyNoise.PointData initialPointData = GetPointData(cell.position);
            DiscoverPointsRecursively(cell.position, initialPointData);
        }

        void AddBufferFromMatrix()
        {
            DynamicBuffer<WorleyNoise.PointData> worleyBuffer = commandBuffer.AddBuffer<WorleyNoise.PointData>(cellEntity);
            worleyBuffer.CopyFrom(matrix.matrix);
        }

        WorleyCellSystem.CellMatrix AddCellMatrixComponent()
        {
            WorleyCellSystem.CellMatrix cellMatrix = new WorleyCellSystem.CellMatrix{
                root = matrix.rootPosition,
                width = matrix.width
            };
            commandBuffer.AddComponent<WorleyCellSystem.CellMatrix>(cellEntity, cellMatrix);
            return cellMatrix;
        }

        void SetPosition(float3 position)
        {
            float3 pos = new float3(position.x, 0, position.z);
            commandBuffer.SetComponent(cellEntity, new Translation{ Value = pos });
        }

        void DiscoverPointsRecursively(float3 position, WorleyNoise.PointData data)
        {
            matrix.AddItem(data, position);

            bool currentInCell = data.currentCellValue == cell.value;

            for(int x = -1; x <= 1; x++)
                for(int z = -1; z <= 1; z++)
                {
                    if(x + z == 0) continue;

                    float3 adjacent = new float3(x, 0, z) + position;

                    WorleyNoise.PointData adjacentData = GetPointData(adjacent);

                    bool adjacentInCell = adjacentData.currentCellValue == cell.value;

                    if(matrix.ItemIsSet(adjacent) || (!currentInCell && !adjacentInCell)) continue;

                    DiscoverPointsRecursively(adjacent, adjacentData);
                }
        }

        WorleyNoise.PointData GetPointData(float3 position)
        {
            WorleyNoise.PointData data = worley.GetPointData(position.x, position.z);
            data.pointWorldPosition = position;
            data.isSet = 1;
            return data;
        }
    }
}
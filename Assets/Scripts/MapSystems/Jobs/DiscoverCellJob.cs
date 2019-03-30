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
            DynamicBuffer<WorleyNoise.PointData> worleyBuffer = commandBuffer.AddBuffer<WorleyNoise.PointData>(cellEntity);
            Discover(cell.position);
            worleyBuffer.CopyFrom(matrix.matrix);

            WorleyCellSystem.CellMatrix CellMatrix = new WorleyCellSystem.CellMatrix{
                root = matrix.rootPosition,
                width = matrix.width
            };
            commandBuffer.AddComponent<WorleyCellSystem.CellMatrix>(cellEntity, CellMatrix);

            float3 pos = new float3(CellMatrix.root.x, 0, CellMatrix.root.z);
            commandBuffer.SetComponent(cellEntity, new Translation{ Value = pos });
        }

        void Discover(float3 position)
        {
            WorleyNoise.PointData data = worley.GetPointData(position.x, position.z);
            data.pointWorldPosition = position;
            data.isSet = 1;

            if(matrix.ItemIsSet(position) || data.currentCellValue != cell.value)
                return;

            matrix.AddItem(data, position);

            for(int x = -1; x <= 1; x++)
                for(int z = -1; z <= 1; z++)
                {
                    if(x + z == 0) continue;

                    float3 adjacent = new float3(x, 0, z) + position;

                    Discover(adjacent);
                }
        }
    }
}
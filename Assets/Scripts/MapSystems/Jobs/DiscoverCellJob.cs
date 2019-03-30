using Unity.Jobs;
using Unity.Mathematics;
using Unity.Entities;

namespace MapGeneration
{
    public struct DiscoverCellJob : IJob
    {
        public Matrix<WorleyNoise.PointData> matrix;
        public WorleyNoise worley;
        public WorleyNoise.CellData cell;
        public float3 startPosition;

        public void Execute()
        {

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
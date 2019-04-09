using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using Unity.Mathematics;

namespace Tests
{
    public class WorleyNoiseTests
    {
        WorleyNoise cellWorley = TerrainSettings.CellWorley();

        [Test]
        public void GetPointData_returns_values_greater_than_zero()
        {
            WorleyNoise.PointData point = RandomPointData();

            float sumOfAllValues = 0;

            sumOfAllValues += point.distance2Edge;
            sumOfAllValues += point.distance;

            sumOfAllValues += point.currentCellValue;
            sumOfAllValues += point.adjacentCellValue;

            sumOfAllValues += point.currentCellPosition.x;
            sumOfAllValues += point.currentCellPosition.z;
            sumOfAllValues += point.adjacentCellPosition.x;
            sumOfAllValues += point.adjacentCellPosition.z;

            sumOfAllValues += point.currentCellIndex.x;
            sumOfAllValues += point.currentCellIndex.y;
            sumOfAllValues += point.adjacentCellIndex.x;
            sumOfAllValues += point.adjacentCellIndex.y;

            Assert.NotZero(sumOfAllValues);
        }

        [Test]
        public void Adjacent_position_matches_adjacent_cell_current_position()
        {
            WorleyNoise.PointData randomPoint = RandomPointData();
            WorleyNoise.CellData cell = cellWorley.GetCellData(randomPoint.currentCellIndex);

            Assert.IsTrue(randomPoint.currentCellPosition.Equals(cell.position));
        }

        WorleyNoise.PointData RandomPointData()
        {
            Unity.Mathematics.Random random = new Unity.Mathematics.Random(123456);

            int x = random.NextInt(0, 500);
            int z = random.NextInt(0, 500);

            return cellWorley.GetPointData(x, z);
        }
        
    }
}


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
        TestUtility testUtil = new TestUtility();

        [Test]
        public void Cell_value_greater_than_zero_and_less_than_one()
        {
            for(int i = 0; i < 500; i++)
            {
                WorleyNoise.CellData cell = testUtil.RandomCellData(cellWorley);
                WorleyNoise.PointData point = testUtil.RandomPointData(cellWorley);

                Assert.Less(cell.value, 1, "Cell less than 1");
                Assert.Greater(cell.value, 0, "Cell greater than 0");
                Assert.Less(point.currentCellValue, 1, "Point less than 1");
                Assert.Greater(point.currentCellValue, 0, "Point greater than 0");
            }
        }

        [Test]
        public void Returns_values_greater_than_zero()
        {
            WorleyNoise.CellData cell = testUtil.RandomCellData(cellWorley);
            WorleyNoise.PointData point = testUtil.RandomPointData(cellWorley);

            float sumOfAllValuesPoint = 0;
            float sumOfAllValuesCell = 0;

            sumOfAllValuesCell += cell.index.x;
            sumOfAllValuesCell += cell.index.y;
            sumOfAllValuesCell += cell.position.x;
            sumOfAllValuesCell += cell.position.z;
            sumOfAllValuesCell += cell.value;

            sumOfAllValuesPoint += point.distance2Edge;
            sumOfAllValuesPoint += point.distance;

            sumOfAllValuesPoint += point.currentCellValue;
            sumOfAllValuesPoint += point.adjacentCellValue;

            sumOfAllValuesPoint += point.currentCellPosition.x;
            sumOfAllValuesPoint += point.currentCellPosition.z;
            sumOfAllValuesPoint += point.adjacentCellPosition.x;
            sumOfAllValuesPoint += point.adjacentCellPosition.z;

            sumOfAllValuesPoint += point.currentCellIndex.x;
            sumOfAllValuesPoint += point.currentCellIndex.y;
            sumOfAllValuesPoint += point.adjacentCellIndex.x;
            sumOfAllValuesPoint += point.adjacentCellIndex.y;

            Assert.NotZero(sumOfAllValuesPoint, "Point");
            Assert.NotZero(sumOfAllValuesCell, "Cell");
        }

        [Test]
        public void Distances_do_not_equal_999999()
        {
            WorleyNoise.PointData point = testUtil.RandomPointData(cellWorley);

            bool notEqual = (   point.distance2Edge != 999999   &&
                                point.distance      != 999999   );

            Assert.IsTrue(notEqual, "distance2Edge: "+point.distance2Edge+"\ndistance: "+point.distance);
        }

        [Test]
        public void PointData_matches_CellData()
        {
            WorleyNoise.PointData randomPoint = testUtil.RandomPointData(cellWorley);
            WorleyNoise.CellData cell = cellWorley.GetCellData(randomPoint.currentCellIndex);

            Assert.IsTrue(
                randomPoint.currentCellPosition.Equals(cell.position),
                "Position\n"+"PointData: "+randomPoint.currentCellPosition+'\n'+"CellData: "+cell.position
            );
            Assert.IsTrue(
                randomPoint.currentCellIndex.Equals(cell.index),
                "Index\n"+"PointData: "+randomPoint.currentCellIndex+'\n'+"CellData: "+cell.index
            );
            Assert.IsTrue(
                randomPoint.currentCellValue.Equals(cell.value),
                "Value\n"+"PointData: "+randomPoint.currentCellValue+'\n'+"CellData: "+cell.value
            );
        }
    }
}


using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using Unity.Mathematics;
using MapGeneration;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;


namespace Tests
{
    public class FloodFillCellJobTests
    {
        WorleyNoise cellWorley = TerrainSettings.CellWorley();
        TestUtility testUtil = new TestUtility();

        [Test]
        public void FloodFillCell_generates_some_data()
        {
            FloodFillCellJob floodFillJob = RandomCellJob();
            floodFillJob.FloodFillCell();

            bool wasGenerated = false;
            for(int i = 0; i < floodFillJob.matrix.Length; i++)
                if(floodFillJob.matrix.GetItem(i).isSet > 0)
                {
                    wasGenerated = true;
                    break;
                }

            Assert.IsTrue(wasGenerated);
        }

        [Test]
        public void Only_points_in_cell_and_adjacent_are_filled()
        {
            FloodFillCellJob floodFillJob = RandomCellJob();
            floodFillJob.FloodFillCell();

            int2 cellIndex = floodFillJob.cell.index;

            List<WorleyNoise.PointData> allPoints = new List<WorleyNoise.PointData>();
            for(int i = 0; i < floodFillJob.matrix.Length; i++)
            {
                WorleyNoise.PointData point =  floodFillJob.matrix.GetItem(i);
                if(point.isSet > 0)
                {
                    allPoints.Add(point);
                }
            }

            List<WorleyNoise.PointData> uniqueCellPoints = testUtil.Set<WorleyNoise.PointData>(allPoints);
            
            bool onlyAdjacent = true;
            foreach(WorleyNoise.PointData point in uniqueCellPoints)
            {
                if(point.currentCellIndex.Equals(cellIndex)) continue;

                int2 adjacentOffset = math.abs(point.currentCellIndex - cellIndex);
                if(adjacentOffset.x > 1 || adjacentOffset.y > 1)
                {
                    onlyAdjacent = false;
                    break;
                }
            }

            Assert.IsTrue(onlyAdjacent);
        }

        FloodFillCellJob RandomCellJob()
        {
            WorleyNoise.CellData cell = testUtil.RandomCellData(cellWorley);

            return new FloodFillCellJob{
                commandBuffer = new EntityCommandBuffer(Allocator.Persistent),
                cellEntity = new Entity(),
                matrix = new Matrix<WorleyNoise.PointData>(10, Allocator.TempJob, cell.position, job: true),
                worley = cellWorley,
                cell = cell
            };
        }


    }
}

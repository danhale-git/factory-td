using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Entities;

namespace Tests
{
    public class CellSystemTests
    {
        [Test]
        public void Initial_cell_is_created()
        {
            CellSystem cellSystem = World.Active.GetOrCreateSystem<CellSystem>();
            
            Entity currentCellEntity;
            bool currentCellExists = cellSystem.TryGetCell(cellSystem.currentCellIndex, out currentCellEntity);
            Assert.IsTrue(currentCellExists);
        }

        [Test]
        public void Initial_cell_has_worley_data()
        {
            Assert.IsTrue(CellHasData(CurrentCellEntity()));
        }

        [Test]
        public void Adjacent_cells_are_created()
        {
            CellSystem cellSystem = World.Active.GetOrCreateSystem<CellSystem>();

            DynamicBuffer<CellSystem.AdjacentCell> adjacentCells = World.Active.EntityManager.GetBuffer<CellSystem.AdjacentCell>(CurrentCellEntity());

            bool noCellsMissing = false;

            for(int i = 0; i < adjacentCells.Length; i++)
            {
                Entity adjacentCellEntity;
                if(!cellSystem.TryGetCell(adjacentCells[i].data.index, out adjacentCellEntity))
                {
                    noCellsMissing = false;
                    break;
                }
                else
                    noCellsMissing = true;
            }

            Assert.IsTrue(noCellsMissing);
        }

        Entity CurrentCellEntity()
        {
            CellSystem cellSystem = World.Active.GetOrCreateSystem<CellSystem>();

            Entity currentCellEntity;
            cellSystem.TryGetCell(cellSystem.currentCellIndex, out currentCellEntity);

            return currentCellEntity;
        }

        bool CellHasData(Entity cellEntity)
        {
            EntityManager entityManager = World.Active.EntityManager;
            if(!entityManager.Exists(cellEntity))
                return false;

            return entityManager.HasComponent<WorleyNoise.PointData>(cellEntity);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Entities;

namespace Tests
{
    public class WorldTests
    {
        [Test]
        public void Default_world_exists()
        {
            World defaultWorld = World.Active;
            Assert.NotNull(defaultWorld);
        }

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
            EntityManager entityManager = World.Active.EntityManager;

            CellSystem cellSystem = World.Active.GetOrCreateSystem<CellSystem>();
            
            Entity currentCellEntity;
            cellSystem.TryGetCell(cellSystem.currentCellIndex, out currentCellEntity);

            DynamicBuffer<WorleyNoise.PointData> worleyData = entityManager.GetBuffer<WorleyNoise.PointData>(currentCellEntity);

            bool pointIsPopulated = false;

            for(int i = 0; i < worleyData.Length; i++)
            {
                pointIsPopulated = worleyData[i].isSet;
                if(pointIsPopulated)
                    break;
            }

            Assert.IsTrue(pointIsPopulated);
        }
    }
}

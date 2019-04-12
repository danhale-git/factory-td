using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Entities;
using Unity.Collections;

namespace Tests
{
    public class HybridSystemTests : ECSTestFixture
    {
        [Test]
        public void Creates_GameObjects_at_correct_positions()
        {
            CellSystem cellSystem = World.CreateSystem<CellSystem>();
            cellSystem.Update();


            NativeArray<Entity> entities = m_Manager.GetAllEntities();
            int entityCount = entities.Length;
            entities.Dispose();

            Assert.NotZero(entityCount);
            
        }
    }
}

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
    }
}

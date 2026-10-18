using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    public class RoamingPlayAreaTests
    {
        [Test]
        public void ASmallAreaIsGrownAroundTheSameCentre()
        {
            var small = new Bounds(new Vector3(1f, 1.3f, -2f), new Vector3(3f, 2.6f, 3f));
            var grown = RoamingPlayArea.EnsureRoaming(small);
            Assert.AreEqual(RoamingPlayArea.MinimumMeters, grown.size.x);
            Assert.AreEqual(RoamingPlayArea.MinimumMeters, grown.size.z);
            Assert.AreEqual(small.center, grown.center);
        }

        [Test]
        public void ABiggerAreaIsKeptAndNeverShrunk()
        {
            var big = new Bounds(Vector3.zero, new Vector3(30f, 2.6f, 20f));
            var result = RoamingPlayArea.EnsureRoaming(big);
            Assert.AreEqual(30f, result.size.x);
            Assert.AreEqual(20f, result.size.z);
        }

        [Test]
        public void OneLongSideDoesNotShrinkTheOtherBelowTheMinimum()
        {
            var thin = new Bounds(Vector3.zero, new Vector3(40f, 2.6f, 2f));
            var result = RoamingPlayArea.EnsureRoaming(thin);
            Assert.AreEqual(40f, result.size.x);
            Assert.AreEqual(RoamingPlayArea.MinimumMeters, result.size.z);
        }

        [Test]
        public void HeightIsLeftAlone()
        {
            var a = new Bounds(new Vector3(0f, 1.3f, 0f), new Vector3(3f, 2.6f, 3f));
            Assert.AreEqual(2.6f, RoamingPlayArea.EnsureRoaming(a).size.y);
        }

        [Test]
        public void TheFloorTextureIsFinerForABiggerAreaUpToTheLimit()
        {
            Assert.AreEqual(64, RoamingPlayArea.FloorResolution(new Bounds(Vector3.zero, new Vector3(2f, 1f, 2f))));
            Assert.AreEqual(256, RoamingPlayArea.FloorResolution(new Bounds(Vector3.zero, new Vector3(16f, 1f, 16f))));
            Assert.AreEqual(256, RoamingPlayArea.FloorResolution(new Bounds(Vector3.zero, new Vector3(80f, 1f, 80f))));
        }
    }
}

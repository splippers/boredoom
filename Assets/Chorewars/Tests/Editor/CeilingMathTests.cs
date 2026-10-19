using System.Collections.Generic;
using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    public class CeilingMathTests
    {
        [Test]
        public void AGazeStraightUpMeetsTheCeilingAboveTheHead()
        {
            Assert.IsTrue(CeilingMath.TryHit(new Vector3(1, 1.6f, 2), Vector3.up, 2.5f, out var p));
            Assert.AreEqual(new Vector3(1, 2.5f, 2), p);
        }

        [Test]
        public void AngledGazeLandsFartherAway()
        {
            var dir = new Vector3(0, 1, 1).normalized;   // 45 degrees up, forward
            Assert.IsTrue(CeilingMath.TryHit(new Vector3(0, 1.5f, 0), dir, 2.5f, out var p));
            Assert.AreEqual(1f, p.z, 0.001f);
        }

        [Test]
        public void LevelOrDownwardGazeDoesNotHit()
        {
            Assert.IsFalse(CeilingMath.TryHit(Vector3.up, Vector3.forward, 2.5f, out _));
            Assert.IsFalse(CeilingMath.TryHit(Vector3.up, Vector3.down, 2.5f, out _));
        }

        [Test]
        public void OutlineIsTheBoxAroundTheCorners()
        {
            var r = CeilingMath.Outline(new List<Vector3> { new Vector3(0, 2.5f, 0), new Vector3(4, 2.5f, 0.2f), new Vector3(4.1f, 2.5f, 3), new Vector3(-0.1f, 2.5f, 3) });
            Assert.AreEqual(-0.1f, r.xMin, 0.001f);
            Assert.AreEqual(4.1f, r.xMax, 0.001f);
            Assert.AreEqual(3f, r.yMax, 0.001f);
        }

        [Test]
        public void LookingBackAtTheFirstCornerClosesTheLoopOnlyAfterThree()
        {
            var two = new List<Vector3> { Vector3.zero, new Vector3(3, 0, 0) };
            var four = new List<Vector3> { Vector3.zero, new Vector3(3, 0, 0), new Vector3(3, 0, 3), new Vector3(0, 0, 3) };
            Assert.IsFalse(CeilingMath.ClosesLoop(two, new Vector3(0.1f, 0, 0)));
            Assert.IsTrue(CeilingMath.ClosesLoop(four, new Vector3(0.1f, 0, 0.1f)));
            Assert.IsFalse(CeilingMath.ClosesLoop(four, new Vector3(2, 0, 2)));
        }

        [Test]
        public void AMeasuredRoomIsNeverStretchedToCloseAGap()
        {
            var a = new RoomScan { roomId = "a", boundsXZ = Rect.MinMaxRect(0, 0, 2, 2) };
            var b = new RoomScan { roomId = "b", boundsXZ = Rect.MinMaxRect(5, 0, 7, 2) };
            var rooms = new List<RoomScan> { a, b };

            HouseMapBuilder.CloseGaps(rooms, 6f, new HashSet<string> { "a" });

            Assert.AreEqual(2f, rooms[0].boundsXZ.xMax, 0.001f, "the measured room keeps its walls");
            Assert.AreEqual(2f, rooms[1].boundsXZ.xMin, 0.001f, "the other room takes the whole gap");
        }
    
        [Test]
        public void TwoLooksAtTheSameSpotGiveTheCeilingHeight()
        {
            var spot = new Vector3(1f, 2.7f, 4f);
            var a = new Vector3(0, 1.6f, 0);
            var b = new Vector3(2.5f, 1.6f, 0);
            Assert.IsTrue(CeilingMath.TryHeightFromTwoGazes(a, spot - a, b, spot - b, out var h));
            Assert.AreEqual(2.7f, h, 0.01f);
        }

        [Test]
        public void TwoLooksFromTheSamePlaceGiveNoHeight()
        {
            var spot = new Vector3(1f, 2.7f, 4f);
            var a = new Vector3(0, 1.6f, 0);
            Assert.IsFalse(CeilingMath.TryHeightFromTwoGazes(a, spot - a, a + new Vector3(0.2f, 0, 0), spot - a, out _));
        }

        [Test]
        public void TwoLooksAtDifferentSpotsAreRejected()
        {
            var a = new Vector3(0, 1.6f, 0);
            var b = new Vector3(2.5f, 1.6f, 0);
            Assert.IsFalse(CeilingMath.TryHeightFromTwoGazes(a, new Vector3(1f, 2.7f, 4f) - a, b, new Vector3(3f, 2.7f, 6f) - b, out _));
        }
}
}

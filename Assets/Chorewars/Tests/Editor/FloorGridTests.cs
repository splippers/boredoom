using System.Collections.Generic;
using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    public class FloorGridTests
    {
        static FloorGrid Room() => new FloorGrid(new Rect(0, 0, 4, 3), 0.5f);

        [Test]
        public void ARoomStartsEntirelyUnknown()
        {
            var g = Room();
            Assert.AreEqual(8, g.Columns);
            Assert.AreEqual(6, g.Rows);
            Assert.AreEqual(48, g.Count(FloorCell.Unknown));
        }

        [Test]
        public void WalkingMarksCellsNearbyAsFree()
        {
            var g = Room();
            Assert.Greater(g.MarkWalked(new Vector2(2, 1.5f), 0.6f), 0);
            Assert.AreEqual(FloorCell.Free, g[4, 3]);
            Assert.AreEqual(FloorCell.Unknown, g[0, 0]);
        }

        [Test]
        public void FurnitureBlocksCellsEvenWhereTheyWalkedAndWalkingCannotUnblockThem()
        {
            var g = Room();
            g.MarkWalked(new Vector2(2, 1.5f), 1.0f);
            var sofa = new List<Vector2> { new Vector2(1, 1), new Vector2(3, 1), new Vector2(3, 2), new Vector2(1, 2) };
            Assert.Greater(g.MarkBlocked(sofa), 0);
            Assert.AreEqual(FloorCell.Blocked, g[4, 3]);
            g.MarkWalked(new Vector2(2, 1.5f), 1.0f);
            Assert.AreEqual(FloorCell.Blocked, g[4, 3]);
        }

        [Test]
        public void AreaAddsUp()
        {
            var g = Room();
            g.MarkBlocked(new List<Vector2> { new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 2), new Vector2(0, 2) });
            Assert.AreEqual(4f, g.AreaOf(FloorCell.Blocked), 0.01f);
        }

        [Test]
        public void APointOutsideTheRoomIsNotACell()
        {
            Assert.IsFalse(Room().TryCell(new Vector2(-1, 1), out _, out _));
            Assert.IsFalse(Room().TryCell(new Vector2(1, 4), out _, out _));
        }

        [Test]
        public void EncodeShowsTheThreeStates()
        {
            var g = new FloorGrid(new Rect(0, 0, 1.5f, 0.5f), 0.5f);
            g.MarkWalked(new Vector2(0.25f, 0.25f), 0.1f);
            g.MarkBlocked(new List<Vector2> { new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f) });
            Assert.AreEqual("o#.", g.Encode());
        }
    }
}

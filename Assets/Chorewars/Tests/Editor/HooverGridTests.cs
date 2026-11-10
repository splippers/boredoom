using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    public class HooverGridTests
    {
        static readonly Vector3[] Square = { new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(2, 0, 2), new Vector3(0, 0, 2) };
        static readonly Vector3[] LShape = { new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(2, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, 2), new Vector3(0, 0, 2) };

        [Test] public void ContainsHandlesInsideOutsideAndConcave()
        {
            Assert.IsTrue(HooverGrid.Contains(Square, 1f, 1f));
            Assert.IsFalse(HooverGrid.Contains(Square, 3f, 1f));
            Assert.IsTrue(HooverGrid.Contains(LShape, 0.5f, 1.5f));
            Assert.IsFalse(HooverGrid.Contains(LShape, 1.5f, 1.5f), "the notch of an L is outside");
        }

        [Test] public void CellsCoverTheSquareExactly() => Assert.AreEqual(64, HooverGrid.Cells(Square, 0.25f).Count);

        [Test] public void CleanMarksNearbyCellsOnceOnly()
        {
            var cells = HooverGrid.Cells(Square, 0.25f); var clean = new bool[cells.Count];
            int first = HooverGrid.Clean(cells, clean, 1f, 1f, 0.35f);
            Assert.Greater(first, 0);
            Assert.AreEqual(0, HooverGrid.Clean(cells, clean, 1f, 1f, 0.35f), "same spot cleans nothing new");
            Assert.AreEqual(cells.Count, HooverGrid.Clean(cells, clean, 1f, 1f, 5f) + first);
        }

        [Test] public void DegeneratePolygonGivesNoCells() => Assert.AreEqual(0, HooverGrid.Cells(new[] { Vector3.zero, Vector3.one }, 0.25f).Count);
    }
}

using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    public class TwoHandGrabTests
    {
        static readonly Vector3 L = new Vector3(-0.1f, 1f, 0.5f);
        static readonly Vector3 R = new Vector3(0.1f, 1f, 0.5f);

        [Test]
        public void NothingHappensUntilBothHandsPinch()
        {
            var g = new TwoHandGrab();
            Assert.IsFalse(g.Update(true, false, L, R));
            Assert.IsFalse(g.Update(false, true, L, R));
            Assert.IsFalse(g.Update(false, false, L, R));
            Assert.IsFalse(g.Active);
        }

        [Test]
        public void TheFirstFrameOnlyAnchors()
        {
            var g = new TwoHandGrab();
            Assert.IsTrue(g.Update(true, true, L, R));
            Assert.AreEqual(1f, g.Factor);
            Assert.AreEqual(0f, g.YawDegrees);
            Assert.AreEqual(Vector3.zero, g.Move);
        }

        [Test]
        public void SpreadingTheHandsScalesUpAndSqueezingScalesDown()
        {
            var g = new TwoHandGrab();
            g.Update(true, true, L, R);
            g.Update(true, true, new Vector3(-0.2f, 1f, 0.5f), new Vector3(0.2f, 1f, 0.5f));
            Assert.AreEqual(2f, g.Factor, 1e-4f);

            g.Update(true, true, new Vector3(-0.05f, 1f, 0.5f), new Vector3(0.05f, 1f, 0.5f));
            Assert.AreEqual(0.5f, g.Factor, 1e-4f);
        }

        [Test]
        public void TurningTheHandsAboutTheirMiddleTurnsTheHologram()
        {
            var g = new TwoHandGrab();
            g.Update(true, true, L, R);
            // The right hand moves forward and the left back: a quarter turn.
            g.Update(true, true, new Vector3(0f, 1f, 0.4f), new Vector3(0f, 1f, 0.6f));
            Assert.AreEqual(90f, Mathf.Abs(g.YawDegrees), 1e-2f);
        }

        [Test]
        public void CarryingBothHandsMovesItWithoutScalingOrTurning()
        {
            var g = new TwoHandGrab();
            g.Update(true, true, L, R);
            var shift = new Vector3(0.3f, -0.1f, 0.2f);
            g.Update(true, true, L + shift, R + shift);
            Assert.AreEqual(1f, g.Factor, 1e-4f);
            Assert.AreEqual(0f, g.YawDegrees, 1e-3f);
            Assert.AreEqual(shift.x, g.Move.x, 1e-4f);
            Assert.AreEqual(shift.y, g.Move.y, 1e-4f);
            Assert.AreEqual(shift.z, g.Move.z, 1e-4f);
        }

        [Test]
        public void LiftingTheHandsWithoutTurningDoesNotCountAsATurn()
        {
            var g = new TwoHandGrab();
            g.Update(true, true, L, R);
            g.Update(true, true, L + Vector3.up * 0.3f, R + Vector3.up * 0.6f); // tilted, but level on the floor plane
            Assert.AreEqual(0f, g.YawDegrees, 1e-3f);
        }

        [Test]
        public void OpeningEitherHandEndsItAndTheNextGrabStartsFresh()
        {
            var g = new TwoHandGrab();
            g.Update(true, true, L, R);
            g.Update(true, true, L * 2f, R * 2f);
            Assert.AreNotEqual(1f, g.Factor);

            Assert.IsFalse(g.Update(true, false, L * 2f, R * 2f));
            Assert.AreEqual(1f, g.Factor);

            g.Update(true, true, L * 2f, R * 2f);   // a new grab at the wider gap
            Assert.AreEqual(1f, g.Factor, 1e-4f, "measured from where this grab began");
        }

        [Test]
        public void HandsTouchingDoNotMakeAnInfiniteScale()
        {
            var g = new TwoHandGrab();
            g.Update(true, true, L, R);
            g.Update(true, true, Vector3.zero, Vector3.zero);
            Assert.Greater(g.Factor, 0f);
            Assert.Less(g.Factor, 1f);
            Assert.IsFalse(float.IsNaN(g.Factor) || float.IsInfinity(g.Factor));
        }
    }
}

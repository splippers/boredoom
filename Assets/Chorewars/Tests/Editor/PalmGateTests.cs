using Chorewars.Runtime;
using NUnit.Framework;

namespace Chorewars.Tests
{
    public class PalmGateTests
    {
        const float Dt = 0.05f;

        static bool Run(PalmGate g, float normalUp, bool tracked, float seconds)
        {
            bool v = g.Visible;
            for (float t = 0; t < seconds; t += Dt) v = g.Update(normalUp, tracked, Dt);
            return v;
        }

        [Test]
        public void StartsHidden()
        {
            Assert.IsFalse(new PalmGate().Visible);
        }

        [Test]
        public void ShowsOnlyAfterThePalmHasBeenUpForAMoment()
        {
            var g = new PalmGate();
            Assert.IsFalse(Run(g, 0.9f, true, 0.1f), "a flash of palm-up is not enough");
            Assert.IsTrue(Run(g, 0.9f, true, 0.3f));
        }

        [Test]
        public void ALoweredPalmHidesItAfterAMoment()
        {
            var g = new PalmGate();
            Run(g, 0.9f, true, 0.5f);
            Assert.IsTrue(Run(g, 0.0f, true, 0.2f), "still showing during a brief dip");
            Assert.IsFalse(Run(g, 0.0f, true, 0.5f));
        }

        [Test]
        public void ATiltBetweenTheThresholdsChangesNothing()
        {
            var g = new PalmGate();
            Run(g, 0.9f, true, 0.5f);
            Assert.IsTrue(Run(g, 0.45f, true, 5f), "wobbling in the middle must not hide it");

            var h = new PalmGate();
            Assert.IsFalse(Run(h, 0.45f, true, 5f), "and must not show it either");
        }

        [Test]
        public void LosingTheHandHidesItButAGlitchDoesNot()
        {
            var g = new PalmGate();
            Run(g, 0.9f, true, 0.5f);
            Assert.IsTrue(Run(g, 0.9f, false, 0.2f), "one lost frame or two is a glitch");
            Assert.IsFalse(Run(g, 0.9f, false, 0.6f));
        }

        [Test]
        public void AnUpPalmWithoutTrackingNeverShows()
        {
            var g = new PalmGate();
            Assert.IsFalse(Run(g, 1f, false, 3f));
        }

        [Test]
        public void ABriefGlitchDoesNotRestartTheShowDelay()
        {
            var g = new PalmGate();
            Run(g, 0.9f, true, 0.15f);
            Run(g, 0.45f, true, 0.05f); // a wobble in the middle resets the clock
            Assert.IsFalse(Run(g, 0.9f, true, 0.15f));
            Assert.IsTrue(Run(g, 0.9f, true, 0.2f));
        }

        [Test]
        public void ResetHidesIt()
        {
            var g = new PalmGate();
            Run(g, 0.9f, true, 1f);
            g.Reset();
            Assert.IsFalse(g.Visible);
        }
    }
}

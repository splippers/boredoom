using Chorewars.Runtime;
using NUnit.Framework;

namespace Chorewars.Tests
{
    public class PinchZoomTests
    {
        [Test]
        public void StartsAtOne()
        {
            Assert.AreEqual(1f, new PinchZoom().Zoom);
        }

        [Test]
        public void NothingChangesWithoutAPinch()
        {
            var z = new PinchZoom();
            z.Update(false, 0f);
            z.Update(false, 1f);
            Assert.AreEqual(1f, z.Zoom);
            Assert.IsFalse(z.Zooming);
        }

        [Test]
        public void TheFirstPinchFrameOnlyAnchors()
        {
            var z = new PinchZoom();
            z.Update(true, 1.2f);
            Assert.AreEqual(1f, z.Zoom);
            Assert.IsTrue(z.Zooming);
        }

        [Test]
        public void HandUpZoomsInAndDownZoomsOut()
        {
            var up = new PinchZoom();
            up.Update(true, 1f); up.Update(true, 1.2f);
            Assert.Greater(up.Zoom, 1.5f);

            var down = new PinchZoom();
            down.Update(true, 1f); down.Update(true, 0.8f);
            Assert.Less(down.Zoom, 0.7f);
        }

        [Test]
        public void ReleasingKeepsTheSizeAndTheNextPinchCarriesOn()
        {
            var z = new PinchZoom();
            z.Update(true, 1f); z.Update(true, 1.2f);
            float after = z.Zoom;

            z.Update(false, 1.2f);
            Assert.AreEqual(after, z.Zoom, 1e-6f);

            z.Update(true, 0.5f);           // a new pinch low down: anchors, no jump
            Assert.AreEqual(after, z.Zoom, 1e-6f);
            z.Update(true, 0.7f);           // and moving up 20 cm from there zooms in further
            Assert.Greater(z.Zoom, after);
        }

        [Test]
        public void EqualMovesMultiplyByEqualAmounts()
        {
            var a = new PinchZoom();
            a.Update(true, 0f); a.Update(true, 0.1f);
            float step = a.Zoom;                // factor for the first 10 cm

            a.Update(false, 0.1f);
            a.Update(true, 0.1f); a.Update(true, 0.2f);
            Assert.AreEqual(step * step, a.Zoom, 1e-4f);
        }

        [Test]
        public void ZoomIsLimited()
        {
            var z = new PinchZoom();
            z.Update(true, 0f); z.Update(true, 10f);
            Assert.AreEqual(z.MaxZoom, z.Zoom);

            z.Update(false, 0f);
            z.Update(true, 0f); z.Update(true, -10f);
            Assert.AreEqual(z.MinZoom, z.Zoom);
        }

        [Test]
        public void ResetPutsItBack()
        {
            var z = new PinchZoom();
            z.Update(true, 0f); z.Update(true, 0.3f);
            z.Reset();
            Assert.AreEqual(1f, z.Zoom);
            Assert.IsFalse(z.Zooming);
        }
    }
}

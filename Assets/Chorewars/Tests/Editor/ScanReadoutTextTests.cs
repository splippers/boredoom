using Chorewars.Runtime;
using NUnit.Framework;

namespace Chorewars.Tests
{
    public class ScanReadoutTextTests
    {
        [Test]
        public void MissingPermissionIsSaidPlainly()
        {
            var text = ScanReadoutText.Format(false, new int[6], 0, 0, 0);
            StringAssert.Contains("NOT granted", text);
            StringAssert.Contains("yes", text);
        }

        [Test]
        public void CountsAndRoomsAreShown()
        {
            var text = ScanReadoutText.Format(true, new[] { 1, 1, 4, 2, 1, 6 }, 3, 2, 0);
            StringAssert.Contains("Floors 1  Ceilings 1  Walls 4", text);
            StringAssert.Contains("Doors 2  Windows 1  Other 6", text);
            StringAssert.Contains("Rooms 3  Doorways 2", text);
        }

        [Test]
        public void AnEmptyScanAfterRestartsPointsAtSpaceSetup()
        {
            StringAssert.Contains("Space Setup", ScanReadoutText.Advice(true, new int[6], 2));
        }

        [Test]
        public void NoDoorFramesTellsThePlayerToLabelThem()
        {
            StringAssert.Contains("Label doors", ScanReadoutText.Advice(true, new[] { 1, 1, 4, 0, 0, 0 }, 0));
        }

        [Test]
        public void NullCountsDoNotThrow()
        {
            Assert.DoesNotThrow(() => ScanReadoutText.Format(true, null, 0, 0, 0));
        }
    }
}

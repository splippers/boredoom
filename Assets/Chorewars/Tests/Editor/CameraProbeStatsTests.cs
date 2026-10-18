using Chorewars.Diagnostics;
using NUnit.Framework;

namespace Chorewars.Tests
{
    public class CameraProbeStatsTests
    {
        [Test]
        public void NoFramesSaysSo()
        {
            var stats = new CameraProbeStats();
            Assert.AreEqual(0, stats.Frames);
            Assert.AreEqual(0.0, stats.FramesPerSecond);
            Assert.AreEqual("no camera frames received", stats.Describe(0, 0, "?"));
        }

        [Test]
        public void OneFrameHasNoRateYet()
        {
            var stats = new CameraProbeStats();
            stats.Record(5.0);
            Assert.AreEqual(0.0, stats.FramesPerSecond);
        }

        [Test]
        public void RateIsFramesBetweenFirstAndLastOverTheSpan()
        {
            var stats = new CameraProbeStats();
            for (int i = 0; i <= 30; i++) stats.Record(10.0 + i / 30.0); // 31 frames over one second
            Assert.AreEqual(30.0, stats.FramesPerSecond, 1e-9);
        }

        [Test]
        public void FramesAtTheSameInstantGiveNoRate()
        {
            var stats = new CameraProbeStats();
            stats.Record(2.0);
            stats.Record(2.0);
            Assert.AreEqual(0.0, stats.FramesPerSecond);
        }

        [Test]
        public void DescribeReportsSizeFormatAndRate()
        {
            var stats = new CameraProbeStats();
            stats.Record(0.0);
            stats.Record(0.5);
            Assert.AreEqual("2 frames, 1280x960 Rgba32, 2.0 fps", stats.Describe(1280, 960, "Rgba32"));
        }
    }
}

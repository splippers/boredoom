using Chorewars.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    /// <summary>Nothing on the instruction panel may sit on top of anything else.</summary>
    public class PanelLayoutTests
    {
        [Test]
        public void EveryRegionIsInsideTheBackground()
        {
            var regions = PanelLayout.Regions;
            var names = PanelLayout.Names;
            for (int i = 0; i < regions.Length; i++)
            {
                var r = regions[i];
                Assert.GreaterOrEqual(r.xMin, PanelLayout.Background.xMin, names[i] + " pokes out of the left edge");
                Assert.LessOrEqual(r.xMax, PanelLayout.Background.xMax, names[i] + " pokes out of the right edge");
                Assert.GreaterOrEqual(r.yMin, PanelLayout.Background.yMin, names[i] + " pokes out of the bottom edge");
                Assert.LessOrEqual(r.yMax, PanelLayout.Background.yMax, names[i] + " pokes out of the top edge");
            }
        }

        [Test]
        public void NoTwoRegionsOverlap()
        {
            var regions = PanelLayout.Regions;
            var names = PanelLayout.Names;
            for (int i = 0; i < regions.Length; i++)
                for (int j = i + 1; j < regions.Length; j++)
                    Assert.IsFalse(regions[i].Overlaps(regions[j]), names[i] + " overlaps " + names[j]);
        }

        [Test]
        public void ButtonsAreBigEnoughToLookAt()
        {
            // At 1.4 m, a 5.5 degree look radius is about 13 cm; a button should not be smaller than a thumb.
            Assert.GreaterOrEqual(PanelLayout.RepeatButton.width, 0.2f);
            Assert.GreaterOrEqual(PanelLayout.TalkButton.height, 0.08f);
        }

        [Test]
        public void ButtonsAreFarEnoughApartToTellApart()
        {
            float gap = PanelLayout.TalkButton.xMin - PanelLayout.RepeatButton.xMax;
            Assert.GreaterOrEqual(gap, 0.03f);
        }

        [Test]
        public void TheWaveformFitsItsRegion()
        {
            // Bars are 48 wide; each needs room to be drawn as a distinct bar.
            Assert.GreaterOrEqual(PanelLayout.WaveBars.width / 48f, 0.012f);
        }
    }
}

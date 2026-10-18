using Chorewars.Voice;
using NUnit.Framework;

namespace Chorewars.Tests
{
    public class InstructionPanelTests
    {
        // -- DwellButtonLogic -------------------------------------------------------------------

        [Test]
        public void FiresOnceAfterTheDwellTime()
        {
            var b = new DwellButtonLogic { Seconds = 1f };
            Assert.IsFalse(b.Update(true, 0.5f));
            Assert.IsTrue(b.Update(true, 0.6f));
        }

        [Test]
        public void ALongStareIsOnePressNotMany()
        {
            var b = new DwellButtonLogic { Seconds = 1f };
            int presses = 0;
            for (int i = 0; i < 100; i++) if (b.Update(true, 0.1f)) presses++;
            Assert.AreEqual(1, presses);
        }

        [Test]
        public void LookingAwayRearmsTheButton()
        {
            var b = new DwellButtonLogic { Seconds = 1f, DrainSeconds = 0.4f };
            Assert.IsTrue(b.Update(true, 1.1f));
            for (int i = 0; i < 10; i++) b.Update(false, 0.1f);
            Assert.IsTrue(b.Update(true, 1.1f));
        }

        [Test]
        public void AGlanceAwayKeepsMostOfTheProgress()
        {
            var b = new DwellButtonLogic { Seconds = 1f, DrainSeconds = 0.4f };
            b.Update(true, 0.8f);
            b.Update(false, 0.04f); // 10% of the drain time
            Assert.Greater(b.Progress01, 0.6f);
            Assert.IsTrue(b.Update(true, 0.3f));
        }

        [Test]
        public void ProgressNeverGoesNegative()
        {
            var b = new DwellButtonLogic();
            b.Update(false, 5f);
            Assert.AreEqual(0f, b.Progress01);
        }

        // -- TextWrap ----------------------------------------------------------------------------

        [Test]
        public void ShortTextIsOneLine()
        {
            CollectionAssert.AreEqual(new[] { "Walk to the door" }, TextWrap.Lines("Walk to the door", 30, 6));
        }

        [Test]
        public void WrapsAtWordBoundaries()
        {
            var lines = TextWrap.Lines("walk to the kitchen door and say door", 16, 6);
            CollectionAssert.AreEqual(new[] { "walk to the", "kitchen door and", "say door" }, lines);
            foreach (var l in lines) Assert.LessOrEqual(l.Length, 16);
        }

        [Test]
        public void ALongWordIsSplitNotOverflowed()
        {
            var lines = TextWrap.Lines("supercalifragilistic", 8, 6);
            foreach (var l in lines) Assert.LessOrEqual(l.Length, 8);
            Assert.AreEqual("supercalifragilistic", string.Concat(lines));
        }

        [Test]
        public void TooMuchTextIsCutWithAnEllipsis()
        {
            var lines = TextWrap.Lines("one two three four five six seven eight nine ten", 9, 2);
            Assert.AreEqual(2, lines.Count);
            StringAssert.EndsWith("...", lines[1]);
            Assert.LessOrEqual(lines[1].Length, 9);
        }

        [Test]
        public void EmptyTextGivesNoLines()
        {
            Assert.AreEqual(0, TextWrap.Lines("   ", 30, 6).Count);
            Assert.AreEqual(0, TextWrap.Lines(null, 30, 6).Count);
        }
    }
}

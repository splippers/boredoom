using Chorewars.Voice;
using NUnit.Framework;

namespace Chorewars.Tests
{
    public class LevelHistoryTests
    {
        [Test]
        public void SilenceIsAnEmptyBarAndShoutingIsAFullOne()
        {
            Assert.AreEqual(0f, LevelHistory.ToHeight(0f));
            Assert.AreEqual(0f, LevelHistory.ToHeight(0.0005f), 1e-4); // -66 dB: below the floor
            Assert.AreEqual(1f, LevelHistory.ToHeight(0.5f));          // -6 dB: above the ceiling
        }

        [Test]
        public void OrdinarySpeechIsWellUpTheBar()
        {
            Assert.Greater(LevelHistory.ToHeight(0.05f), 0.6f); // -26 dB
            Assert.Less(LevelHistory.ToHeight(0.05f), 0.9f);
        }

        [Test]
        public void LouderIsNeverShorter()
        {
            float last = -1f;
            for (float rms = 0.0001f; rms < 1f; rms *= 1.5f)
            {
                float h = LevelHistory.ToHeight(rms);
                Assert.GreaterOrEqual(h, last);
                last = h;
            }
        }

        [Test]
        public void NewestValueIsOnTheRightAndOldOnesScrollLeft()
        {
            var h = new LevelHistory(4);
            h.Push(0.5f);   // full
            h.Push(0f);
            Assert.AreEqual(1f, h.Height(2), 1e-4, "second newest");
            Assert.AreEqual(0f, h.Height(3), 1e-4, "newest");
        }

        [Test]
        public void UnfilledSlotsReadAsSilence()
        {
            var h = new LevelHistory(8);
            h.Push(0.5f);
            Assert.AreEqual(1f, h.Height(7), 1e-4);
            for (int i = 0; i < 7; i++) Assert.AreEqual(0f, h.Height(i));
            Assert.AreEqual(1, h.Count);
        }

        [Test]
        public void TheOldestFallsOffTheEnd()
        {
            var h = new LevelHistory(3);
            h.Push(0.5f);                 // will be pushed out
            h.Push(0.001f); h.Push(0.001f); h.Push(0.001f);
            for (int i = 0; i < 3; i++) Assert.Less(h.Height(i), 0.2f);
            Assert.AreEqual(3, h.Count);
        }

        [Test]
        public void BadValuesAreMadeSafe()
        {
            var h = new LevelHistory(2);
            h.Push(float.NaN);
            h.Push(-3f);
            Assert.AreEqual(0f, h.Height(0));
            Assert.AreEqual(0f, h.Height(1));
            h.Push(99f);
            Assert.AreEqual(1f, h.Height(1), 1e-4);
        }

        [Test]
        public void ClearForgetsEverything()
        {
            var h = new LevelHistory(3);
            h.Push(0.5f); h.Push(0.5f);
            h.Clear();
            Assert.AreEqual(0, h.Count);
            for (int i = 0; i < 3; i++) Assert.AreEqual(0f, h.Height(i));
        }

        [Test]
        public void OutOfRangeSlotIsSilence()
        {
            var h = new LevelHistory(3);
            h.Push(0.5f);
            Assert.AreEqual(0f, h.Height(-1));
            Assert.AreEqual(0f, h.Height(3));
        }
    }
}

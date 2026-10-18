using Chorewars.Voice;
using NUnit.Framework;

namespace Chorewars.Tests
{
    public class MouthMotionTests
    {
        [Test]
        public void SilenceClosesTheMouth()
        {
            float m = 1f;
            for (int i = 0; i < 30; i++) m = MouthMotion.Next(m, 0f, 0.02f);
            Assert.AreEqual(0f, m, 0.001f);
        }

        [Test]
        public void LoudSpeechOpensItFullyAndNoFurther()
        {
            float m = 0f;
            for (int i = 0; i < 30; i++) m = MouthMotion.Next(m, 0.5f, 0.02f);
            Assert.AreEqual(1f, m, 0.001f);
        }

        [Test]
        public void ItOpensFasterThanItCloses()
        {
            float up = MouthMotion.Next(0f, 1f, 0.01f);
            float down = 1f - MouthMotion.Next(1f, 0f, 0.01f);
            Assert.Greater(up, down);
        }
    }
}

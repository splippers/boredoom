using Chorewars.Voice;
using NUnit.Framework;

namespace Chorewars.Tests
{
    public class ClaudeStatusTests
    {
        [Test]
        public void ThinkingCarriesWhatWasHeard()
        {
            var s = ClaudeStatus.Parse("thinking\tcan you hear me");
            Assert.AreEqual(ClaudeStatus.Kind.Thinking, s.kind);
            Assert.AreEqual("can you hear me", s.detail);
            Assert.IsTrue(s.Busy);
        }

        [Test]
        public void TranscribingNeedsNoDetail()
        {
            Assert.AreEqual(ClaudeStatus.Kind.Transcribing, ClaudeStatus.Parse("transcribing").kind);
        }

        [Test]
        public void IdleUnknownAndEmptyAreAllNothingToShow()
        {
            foreach (var wire in new[] { "idle", "", null, "   ", "dancing\twildly" })
            {
                var s = ClaudeStatus.Parse(wire);
                Assert.IsFalse(s.Busy, wire);
                Assert.AreEqual(string.Empty, s.Describe(1));
            }
        }

        [Test]
        public void KindIsCaseInsensitive()
        {
            Assert.AreEqual(ClaudeStatus.Kind.Thinking, ClaudeStatus.Parse("THINKING\thi").kind);
        }

        [Test]
        public void DotsAnimateBetweenOneAndThree()
        {
            var s = ClaudeStatus.Parse("thinking");
            Assert.AreEqual("Claudia is thinking.", s.Describe(1));
            Assert.AreEqual("Claudia is thinking...", s.Describe(3));
            Assert.AreEqual("Claudia is thinking.", s.Describe(0));
            Assert.AreEqual("Claudia is thinking...", s.Describe(9));
        }

        [Test]
        public void LongDetailIsClipped()
        {
            var s = ClaudeStatus.Parse("thinking\t" + new string('x', 100));
            var line = s.Describe(1, 20);
            StringAssert.EndsWith("...", line);
            Assert.Less(line.Length, 60);
        }

        [Test]
        public void WorkingNamesTheJob()
        {
            StringAssert.Contains("a build", ClaudeStatus.Parse("working\ta build").Describe(2));
        }

        [Test]
        public void StatusFrameHasANameAndAType()
        {
            Assert.AreEqual(0x12, VoiceLinkProtocol.Status);
            Assert.AreEqual("STATUS", VoiceLinkProtocol.Name(VoiceLinkProtocol.Status));
        }
    }
}

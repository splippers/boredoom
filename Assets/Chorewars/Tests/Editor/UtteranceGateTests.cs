using Chorewars.Voice;
using NUnit.Framework;

namespace Chorewars.Tests
{
    public class UtteranceGateTests
    {
        const float Chunk = 0.1f;

        static byte[] Loud() => Pcm(12000);
        static byte[] Quiet() => Pcm(10);

        static byte[] Pcm(short amplitude)
        {
            var bytes = new byte[320]; // 160 samples = 10 ms; level is what matters, not length
            for (int i = 0; i < bytes.Length; i += 2)
            {
                short v = (i / 2) % 2 == 0 ? amplitude : (short)-amplitude;
                bytes[i] = (byte)(v & 0xff);
                bytes[i + 1] = (byte)((v >> 8) & 0xff);
            }
            return bytes;
        }

        [Test]
        public void SilenceIsNeverSent()
        {
            var gate = new UtteranceGate();
            for (int i = 0; i < 100; i++)
            {
                var o = gate.Push(Quiet(), Chunk);
                Assert.AreEqual(0, o.Audio.Count);
                Assert.IsFalse(o.End);
            }
            Assert.IsFalse(gate.InUtterance);
        }

        [Test]
        public void SpeechStartsASentenceAndSendsTheQuietBitBeforeIt()
        {
            var gate = new UtteranceGate { PreRollSeconds = 0.3f };
            for (int i = 0; i < 10; i++) gate.Push(Quiet(), Chunk);

            var o = gate.Push(Loud(), Chunk);

            Assert.IsTrue(gate.InUtterance);
            Assert.GreaterOrEqual(o.Audio.Count, 2, "pre-roll plus the first loud chunk");
            Assert.LessOrEqual(o.Audio.Count, 4, "pre-roll is short, not all of the silence");
        }

        [Test]
        public void SpeechContinuesThenEndsAfterSilence()
        {
            var gate = new UtteranceGate(new EnergyVad(0.02f, 0.5f));
            gate.Push(Loud(), Chunk);
            Assert.AreEqual(1, gate.Push(Loud(), Chunk).Audio.Count);

            bool ended = false;
            for (int i = 0; i < 10 && !ended; i++) ended = gate.Push(Quiet(), Chunk).End;

            Assert.IsTrue(ended);
            Assert.IsFalse(gate.InUtterance);
        }

        [Test]
        public void ALongNoiseIsCutAtTheLimit()
        {
            var gate = new UtteranceGate { MaxUtteranceSeconds = 2f };
            bool cut = false;
            for (int i = 0; i < 40 && !cut; i++) cut = gate.Push(Loud(), Chunk).End;
            Assert.IsTrue(cut);
            Assert.IsFalse(gate.InUtterance);
        }

        [Test]
        public void ANoiseAfterTheCutStartsAFreshSentence()
        {
            var gate = new UtteranceGate { MaxUtteranceSeconds = 1f };
            for (int i = 0; i < 15; i++) gate.Push(Loud(), Chunk); // cut at chunk 10, new sentence from chunk 11, still going at 15
            Assert.IsTrue(gate.InUtterance, "the noise went on, so a new sentence opened after the cut");
        }

        [Test]
        public void StopMidSentenceEndsItAndStopWhileQuietDoesNot()
        {
            var gate = new UtteranceGate();
            gate.Push(Loud(), Chunk);
            Assert.IsTrue(gate.Stop().End);
            Assert.IsFalse(gate.Stop().End);
        }

        [Test]
        public void EmptyInputIsIgnored()
        {
            var gate = new UtteranceGate();
            Assert.AreEqual(0, gate.Push(null, Chunk).Audio.Count);
            Assert.AreEqual(0, gate.Push(new byte[0], Chunk).Audio.Count);
        }
    }
}

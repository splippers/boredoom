using System.Collections.Generic;
using Chorewars.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    /// <summary>
    /// The voice link's pure half: the wire format, the loudness detector, and the sample arithmetic.
    /// All three are checked without a socket, a microphone or a PC, which is the point of keeping them
    /// out of the MonoBehaviours.
    /// </summary>
    public class VoiceLinkProtocolTests
    {
        [Test]
        public void Encode_LaysOutTypeThenBigEndianLengthThenPayload()
        {
            // Byte for byte what the PC side's struct.pack(">BI", kind, len) produces.
            var frame = VoiceLinkProtocol.Encode(VoiceLinkProtocol.Audio, new byte[] { 0xAA, 0xBB });

            CollectionAssert.AreEqual(new byte[] { 0x01, 0x00, 0x00, 0x00, 0x02, 0xAA, 0xBB }, frame);
        }

        [Test]
        public void Encode_OfAnEmptyPayload_IsJustTheHeader()
        {
            CollectionAssert.AreEqual(
                new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00 },
                VoiceLinkProtocol.Encode(VoiceLinkProtocol.End, null));
        }

        [Test]
        public void Encode_OfATextPayload_UsesTheBigEndianLength()
        {
            var payload = System.Text.Encoding.UTF8.GetBytes("Hi");

            var frame = VoiceLinkProtocol.Encode(VoiceLinkProtocol.Text, payload);

            Assert.AreEqual(0x11, frame[0]);
            Assert.AreEqual(2, VoiceLinkProtocol.ReadLength(frame, 0));
            Assert.AreEqual((byte)'H', frame[5]);
        }

        [Test]
        public void ReadLength_OfALargePayload_IsUnsigned()
        {
            // 0x00010000 = 65536; the top bit is clear but it spans two bytes, which is where a
            // signed read would go wrong on bigger numbers.
            var header = new byte[] { 0x10, 0x00, 0x01, 0x00, 0x00 };

            Assert.AreEqual(65536, VoiceLinkProtocol.ReadLength(header, 0));
        }

        [Test]
        public void Reader_ReassemblesAFrameSplitAcrossTwoReads()
        {
            var reader = new VoiceFrameReader();
            var frame = VoiceLinkProtocol.Encode(VoiceLinkProtocol.Text, new byte[] { 1, 2, 3, 4, 5, 6 });

            var none = reader.Feed(frame, 3);
            Assert.AreEqual(0, none.Count, "half a header is not a frame");

            var frames = reader.Feed(Slice(frame, 3), frame.Length - 3);

            Assert.AreEqual(1, frames.Count);
            Assert.AreEqual(VoiceLinkProtocol.Text, frames[0].kind);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6 }, frames[0].payload);
        }

        [Test]
        public void Reader_ReturnsTwoFramesFromOneRead()
        {
            var reader = new VoiceFrameReader();
            var one = VoiceLinkProtocol.Encode(VoiceLinkProtocol.Text, new byte[] { 7 });
            var two = VoiceLinkProtocol.Encode(VoiceLinkProtocol.End, null);

            var joined = new byte[one.Length + two.Length];
            System.Buffer.BlockCopy(one, 0, joined, 0, one.Length);
            System.Buffer.BlockCopy(two, 0, joined, one.Length, two.Length);

            var frames = reader.Feed(joined, joined.Length);

            Assert.AreEqual(2, frames.Count);
            Assert.AreEqual(VoiceLinkProtocol.Text, frames[0].kind);
            Assert.AreEqual(VoiceLinkProtocol.End, frames[1].kind);
            Assert.AreEqual(0, frames[1].Length);
        }

        [Test]
        public void Reader_HandlesAByteAtATime()
        {
            var reader = new VoiceFrameReader();
            var frame = VoiceLinkProtocol.Encode(VoiceLinkProtocol.Wav, new byte[] { 9, 8, 7 });

            var frames = new List<VoiceFrame>();
            for (int i = 0; i < frame.Length; i++)
                frames.AddRange(reader.Feed(new[] { frame[i] }, 1));

            Assert.AreEqual(1, frames.Count);
            Assert.AreEqual(3, frames[0].Length);
        }

        [Test]
        public void Reader_PassesAnUnknownTypeThroughByItsLength()
        {
            // A newer PC could send a type this build has never heard of; the payload still has to be
            // stepped over cleanly so the frame after it is not misread.
            var reader = new VoiceFrameReader();
            var strange = VoiceLinkProtocol.Encode(0x7E, new byte[] { 1, 1, 1, 1 });
            var known = VoiceLinkProtocol.Encode(VoiceLinkProtocol.Text, new byte[] { 42 });

            var joined = new byte[strange.Length + known.Length];
            System.Buffer.BlockCopy(strange, 0, joined, 0, strange.Length);
            System.Buffer.BlockCopy(known, 0, joined, strange.Length, known.Length);

            var frames = reader.Feed(joined, joined.Length);

            Assert.AreEqual(2, frames.Count);
            Assert.AreEqual(0x7E, frames[0].kind);
            Assert.AreEqual(VoiceLinkProtocol.Text, frames[1].kind);
            Assert.AreEqual(42, frames[1].payload[0]);
        }

        [Test]
        public void Reader_FaultsOnAnUnbelievableLength()
        {
            var reader = new VoiceFrameReader();

            // 0xFFFFFFFF as the big-endian length: not a real frame, and reading it would allocate 4 GB.
            var rubbish = new byte[] { 0x10, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };

            var frames = reader.Feed(rubbish, rubbish.Length);

            Assert.AreEqual(0, frames.Count);
            Assert.IsTrue(reader.Faulted, "the stream cannot be trusted from here; the caller reconnects");
        }

        [Test]
        public void Reader_NoticesA_Zero_LengthFrame()
        {
            var reader = new VoiceFrameReader();
            var frame = VoiceLinkProtocol.Encode(VoiceLinkProtocol.End, null);

            var frames = reader.Feed(frame, frame.Length);

            Assert.AreEqual(1, frames.Count);
            Assert.IsNotNull(frames[0].payload);
            Assert.AreEqual(0, frames[0].payload.Length);
        }

        private static byte[] Slice(byte[] source, int from)
        {
            var slice = new byte[source.Length - from];
            System.Buffer.BlockCopy(source, from, slice, 0, slice.Length);
            return slice;
        }
    }

    public class EnergyVadTests
    {
        private const float Frame = 0.1f; // 100 ms, the chunk the microphone is read in

        [Test]
        public void Push_QuietFromCold_NeverEndsAnUtteranceThatNeverStarted()
        {
            var vad = new EnergyVad();

            for (int i = 0; i < 50; i++)
                Assert.AreEqual(VadEvent.Idle, vad.Push(0.001f, Frame), "silence on its own is not speech");
        }

        [Test]
        public void Push_ReportsTheStartOnce()
        {
            var vad = new EnergyVad();

            Assert.AreEqual(VadEvent.Started, vad.Push(0.5f, Frame));
            Assert.AreEqual(VadEvent.Speaking, vad.Push(0.5f, Frame), "still talking is not another start");
            Assert.AreEqual(VadEvent.Speaking, vad.Push(0.5f, Frame));
        }

        [Test]
        public void Push_EndsAfterTheSilenceTimeout()
        {
            var vad = new EnergyVad(threshold: 0.02f, endSilenceSeconds: 0.8f);
            vad.Push(0.5f, Frame); // one frame of speech

            // 0.7 s of quiet: not long enough.
            for (int i = 0; i < 7; i++)
                Assert.AreEqual(VadEvent.Speaking, vad.Push(0.001f, Frame));

            Assert.AreEqual(VadEvent.Ended, vad.Push(0.001f, Frame), "0.8 s of quiet closes the utterance");
            Assert.IsFalse(vad.Speaking);
        }

        [Test]
        public void Push_EndsOnlyOnce()
        {
            var vad = new EnergyVad(endSilenceSeconds: 0.2f);
            vad.Push(0.5f, Frame);
            Assert.AreEqual(VadEvent.Speaking, vad.Push(0.001f, Frame), "0.1 s of quiet is not enough");
            Assert.AreEqual(VadEvent.Ended, vad.Push(0.001f, Frame), "0.2 s closes it");
            Assert.AreEqual(VadEvent.Idle, vad.Push(0.001f, Frame), "a second END would close an empty utterance");
        }

        [Test]
        public void Push_AShortPauseMidSentence_DoesNotEndIt()
        {
            var vad = new EnergyVad(endSilenceSeconds: 0.8f);
            vad.Push(0.5f, Frame);

            for (int i = 0; i < 5; i++) vad.Push(0.001f, Frame); // 0.5 s: a breath between words
            Assert.AreEqual(VadEvent.Speaking, vad.Push(0.5f, Frame), "the utterance is still open");

            Assert.AreEqual(0f, vad.SilenceSeconds, 1e-4f, "the pause counter resets when they speak again");
        }

        [Test]
        public void Push_OnRawPcm_LoudAudioStartsAndSilenceEnds()
        {
            var vad = new EnergyVad(endSilenceSeconds: 0.2f);
            byte[] loud = PcmAudio.FloatToS16le(new[] { 0.6f, -0.6f, 0.6f, -0.6f }, 4);
            byte[] quiet = PcmAudio.FloatToS16le(new[] { 0f, 0f, 0f, 0f }, 4);

            Assert.AreEqual(VadEvent.Started, vad.Push(loud, Frame));
            Assert.AreEqual(VadEvent.Speaking, vad.Push(quiet, Frame));
            Assert.AreEqual(VadEvent.Ended, vad.Push(quiet, Frame));
        }
    }

    public class PcmAudioTests
    {
        [Test]
        public void ToS16_ClipsRatherThanWraps()
        {
            Assert.AreEqual(32767, PcmAudio.ToS16(1f));
            Assert.AreEqual(32767, PcmAudio.ToS16(4f), "a loud sample must not wrap round to negative");
            Assert.AreEqual(-32768, PcmAudio.ToS16(-4f));
            Assert.AreEqual(0, PcmAudio.ToS16(0f));
        }

        [Test]
        public void FloatToS16le_WritesLittleEndianPairs()
        {
            // 0.5 * 32767 = 16383.5 -> 16383 = 0x3FFF, little-endian: 0xFF 0x3F.
            // -1 * 32767 = -32767 = 0x8001, little-endian: 0x01 0x80.
            byte[] bytes = PcmAudio.FloatToS16le(new[] { 0.5f, -1f }, 2);

            CollectionAssert.AreEqual(new byte[] { 0xFF, 0x3F, 0x01, 0x80 }, bytes);
        }

        [Test]
        public void FloatToS16le_RoundTripsThroughS16leToFloat()
        {
            var samples = new[] { 0f, 0.25f, -0.25f, 1f, -1f };

            var back = PcmAudio.S16leToFloat(PcmAudio.FloatToS16le(samples, samples.Length));

            Assert.AreEqual(samples.Length, back.Length);
            for (int i = 0; i < samples.Length; i++)
                Assert.AreEqual(samples[i], back[i], 1e-4f);
        }

        [Test]
        public void Rms_OfSilence_IsZero_AndOfFullScale_IsAboutOne()
        {
            Assert.AreEqual(0f, PcmAudio.Rms(PcmAudio.FloatToS16le(new[] { 0f, 0f, 0f, 0f }, 4)), 1e-6f);

            float loud = PcmAudio.Rms(PcmAudio.FloatToS16le(new[] { 1f, -1f, 1f, -1f }, 4));
            Assert.Greater(loud, 0.9f);
        }

        [Test]
        public void RingDelta_StraightRead_IsTheTailOnly()
        {
            PcmAudio.RingDelta(16000, 1000, 1200, out int offset, out int first, out int second);

            Assert.AreEqual(1000, offset);
            Assert.AreEqual(200, first);
            Assert.AreEqual(0, second);
        }

        [Test]
        public void RingDelta_AcrossTheLoop_ReportsTwoPieces()
        {
            // The microphone clip loops: the write position jumped from 15000 back to 200.
            PcmAudio.RingDelta(16000, 15000, 200, out int offset, out int first, out int second);

            Assert.AreEqual(15000, offset);
            Assert.AreEqual(1000, first, "the tail of the ring");
            Assert.AreEqual(200, second, "then the head");
            Assert.AreEqual(1200, first + second, "1200 samples were written in total");
        }

        [Test]
        public void RingDelta_WithNoNewSamples_ReportsNothing()
        {
            PcmAudio.RingDelta(16000, 500, 500, out _, out int first, out int second);

            Assert.AreEqual(0, first);
            Assert.AreEqual(0, second);
        }

        [Test]
        public void EncodeWav_IsAFileTheDecoderReadsBack()
        {
            var samples = new float[800];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * 0.05f) * 0.4f;

            byte[] wav = PcmAudio.EncodeWav(samples, samples.Length);

            Assert.AreEqual((byte)'R', wav[0]);
            Assert.AreEqual((byte)'I', wav[1]);

            Assert.IsTrue(PcmAudio.TryDecodeWav(wav, out var decoded, out int rate, out int channels));
            Assert.AreEqual(16000, rate);
            Assert.AreEqual(1, channels);
            Assert.AreEqual(samples.Length, decoded.Length);
            Assert.AreEqual(samples[10], decoded[10], 1e-4f);
        }

        [Test]
        public void TryDecodeWav_OnSomethingOtherThanAWav_RefusesIt()
        {
            Assert.IsFalse(PcmAudio.TryDecodeWav(new byte[] { 1, 2, 3 }, out _, out _, out _));
            Assert.IsFalse(PcmAudio.TryDecodeWav(new byte[100], out _, out _, out _));
        }
    }
}

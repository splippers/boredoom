using System;

namespace Chorewars.Voice
{
    /// <summary>
    /// The arithmetic between Unity's audio and the bytes on the wire, kept away from the microphone
    /// so the fiddly parts — sample conversion, the ring buffer wrap, and a .wav file — can be tested
    /// by feeding them numbers.
    /// </summary>
    /// <remarks>
    /// Unity hands out microphone samples as floats in [-1, 1]; the link speaks signed 16-bit
    /// little-endian, 16000 Hz, mono, which is what the PC's speech recogniser expects. Nothing here
    /// touches <c>Microphone</c> or <c>AudioClip</c>.
    /// </remarks>
    public static class PcmAudio
    {
        public const int SampleRate = VoiceLinkProtocol.SampleRate;

        /// <summary>Converts one float sample to signed 16-bit, clipping rather than wrapping.</summary>
        public static short ToS16(float sample)
        {
            float scaled = sample * 32767f;
            if (scaled > 32767f) return 32767;
            if (scaled < -32768f) return -32768;
            return (short)scaled;
        }

        public static byte[] FloatToS16le(float[] samples, int count)
        {
            count = Math.Max(0, Math.Min(count, samples != null ? samples.Length : 0));

            var bytes = new byte[count * 2];
            FloatToS16le(samples, count, bytes, 0);
            return bytes;
        }

        public static void FloatToS16le(float[] samples, int count, byte[] into, int offset)
        {
            if (samples == null || into == null) return;

            count = Math.Max(0, Math.Min(count, samples.Length));
            if (offset + count * 2 > into.Length) return;

            for (int i = 0; i < count; i++)
            {
                short value = ToS16(samples[i]);
                into[offset + i * 2] = (byte)(value & 0xFF);
                into[offset + i * 2 + 1] = (byte)((value >> 8) & 0xFF);
            }
        }

        public static float[] S16leToFloat(byte[] pcm)
        {
            if (pcm == null) return Array.Empty<float>();

            var samples = new float[pcm.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short value = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
                samples[i] = value / 32768f;
            }

            return samples;
        }

        /// <summary>Loudness of a frame, 0..1. What the hands-free detector listens to.</summary>
        public static float Rms(byte[] pcmS16le)
        {
            if (pcmS16le == null || pcmS16le.Length < 2) return 0f;

            int samples = pcmS16le.Length / 2;
            double sum = 0;

            for (int i = 0; i < samples; i++)
            {
                short value = (short)(pcmS16le[i * 2] | (pcmS16le[i * 2 + 1] << 8));
                double normalised = value / 32768.0;
                sum += normalised * normalised;
            }

            return (float)Math.Sqrt(sum / samples);
        }

        /// <summary>
        /// How much of a looping microphone ring buffer has been filled since the last look.
        /// </summary>
        /// <remarks>
        /// A microphone clip of one second loops, so the write position jumps back to the start. The
        /// new samples are then in two pieces — the tail of the ring and the head — and reading them as
        /// one run would replay the whole clip. <paramref name="firstOffset"/> and
        /// <paramref name="firstCount"/> are the tail; <paramref name="secondCount"/> is read from zero.
        /// </remarks>
        public static void RingDelta(int ringLength, int lastPos, int pos,
            out int firstOffset, out int firstCount, out int secondCount)
        {
            firstOffset = 0;
            firstCount = 0;
            secondCount = 0;

            if (ringLength <= 0) return;

            if (lastPos < 0 || lastPos >= ringLength) lastPos = 0;
            if (pos < 0 || pos >= ringLength) pos = 0;

            int count = pos - lastPos;
            if (count < 0) count += ringLength;

            if (count <= 0) return;

            if (count > ringLength) count = ringLength;

            firstOffset = lastPos;
            firstCount = Math.Min(count, ringLength - lastPos);
            secondCount = count - firstCount;
        }

        /// <summary>Builds a complete .wav (PCM 16-bit) from float samples. Used by the tests.</summary>
        public static byte[] EncodeWav(float[] samples, int count, int sampleRate = SampleRate, int channels = 1)
        {
            count = Math.Max(0, Math.Min(count, samples != null ? samples.Length : 0));

            byte[] pcm = FloatToS16le(samples, count);
            int dataBytes = pcm.Length;
            int byteRate = sampleRate * channels * 2;

            var wav = new byte[44 + dataBytes];

            void Ascii(int at, string text)
            {
                for (int i = 0; i < text.Length; i++) wav[at + i] = (byte)text[i];
            }

            void Int32At(int at, int value)
            {
                wav[at] = (byte)value;
                wav[at + 1] = (byte)(value >> 8);
                wav[at + 2] = (byte)(value >> 16);
                wav[at + 3] = (byte)(value >> 24);
            }

            void Int16At(int at, int value)
            {
                wav[at] = (byte)value;
                wav[at + 1] = (byte)(value >> 8);
            }

            Ascii(0, "RIFF");
            Int32At(4, 36 + dataBytes);
            Ascii(8, "WAVE");
            Ascii(12, "fmt ");
            Int32At(16, 16);              // fmt chunk size
            Int16At(20, 1);               // PCM
            Int16At(22, channels);
            Int32At(24, sampleRate);
            Int32At(28, byteRate);
            Int16At(32, channels * 2);    // block align
            Int16At(34, 16);              // bits per sample
            Ascii(36, "data");
            Int32At(40, dataBytes);

            if (dataBytes > 0) Buffer.BlockCopy(pcm, 0, wav, 44, dataBytes);

            return wav;
        }

        /// <summary>
        /// Reads a PCM .wav into floats. Walks the chunk list rather than assuming a 44-byte header,
        /// because writers are free to put anything before <c>data</c>.
        /// </summary>
        public static bool TryDecodeWav(byte[] wav, out float[] samples, out int sampleRate, out int channels)
        {
            samples = Array.Empty<float>();
            sampleRate = 0;
            channels = 0;

            if (wav == null || wav.Length < 44) return false;
            if (!Matches(wav, 0, "RIFF") || !Matches(wav, 8, "WAVE")) return false;

            int bitsPerSample = 0;
            int dataOffset = -1;
            int dataBytes = 0;

            int at = 12;
            while (at + 8 <= wav.Length)
            {
                int size = BitConverter.ToInt32(wav, at + 4);
                int body = at + 8;
                if (size < 0 || body + size > wav.Length) break;

                if (Matches(wav, at, "fmt ") && size >= 16)
                {
                    int format = BitConverter.ToInt16(wav, body);
                    channels = BitConverter.ToInt16(wav, body + 2);
                    sampleRate = BitConverter.ToInt32(wav, body + 4);
                    bitsPerSample = BitConverter.ToInt16(wav, body + 14);

                    if (format != 1) return false; // not PCM
                }
                else if (Matches(wav, at, "data"))
                {
                    dataOffset = body;
                    dataBytes = size;
                }

                at = body + size + (size & 1); // chunks are word-aligned
            }

            if (dataOffset < 0 || bitsPerSample != 16) return false;
            if (channels <= 0 || sampleRate <= 0) return false;

            // Mono downmix: the link is mono, and playing a stereo reply through one voice is wrong but
            // losing half the words is worse.
            int totalSamples = dataBytes / 2;
            int frames = totalSamples / channels;
            samples = new float[frames];

            for (int frame = 0; frame < frames; frame++)
            {
                float sum = 0;
                for (int channel = 0; channel < channels; channel++)
                {
                    int index = dataOffset + (frame * channels + channel) * 2;
                    short value = (short)(wav[index] | (wav[index + 1] << 8));
                    sum += value / 32768f;
                }

                samples[frame] = sum / channels;
            }

            return true;
        }

        private static bool Matches(byte[] data, int at, string tag)
        {
            if (at + tag.Length > data.Length) return false;

            for (int i = 0; i < tag.Length; i++)
                if (data[at + i] != (byte)tag[i]) return false;

            return true;
        }
    }
}

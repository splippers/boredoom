using System.Collections.Generic;

namespace Chorewars.Voice
{
    /// <summary>
    /// The wire format of the headset &lt;-&gt; PC voice link, and the one place it is written down.
    /// </summary>
    /// <remarks>
    /// Both directions send the same shape: one byte of type, four bytes of big-endian length, then
    /// the payload. The PC half is tools/voice/voice_link.py, and it packs the header as
    /// <c>&gt;BI</c>, so the length is unsigned and big-endian. Nothing here knows about sockets: it
    /// turns frames into bytes and bytes back into frames, so the format can be tested against the
    /// Python side by comparing literals rather than by talking to a headset.
    ///
    /// A frame whose type is not understood is not an error. New message types can be added on one
    /// side at a time, and an old reader skips the frame by its length instead of falling over.
    /// </remarks>
    public static class VoiceLinkProtocol
    {
        /// <summary>Headset to PC: raw audio. Signed 16-bit little-endian, 16000 Hz, mono, any chunk size.</summary>
        public const byte Audio = 0x01;

        /// <summary>Headset to PC: the player stopped talking. Transcribe everything since the last one.</summary>
        public const byte End = 0x02;

        /// <summary>PC to headset: a complete .wav file to play.</summary>
        public const byte Wav = 0x10;

        /// <summary>PC to headset: a UTF-8 caption to show alongside the speech.</summary>
        public const byte Text = 0x11;

        /// <summary>PC to headset: what Claude is doing right now (see <see cref="ClaudeStatus"/>).</summary>
        public const byte Status = 0x12;

        public const int HeaderBytes = 5;

        /// <summary>
        /// The longest payload worth believing. A spoken sentence is well under a megabyte; a length
        /// beyond this is a corrupt stream, and continuing to read it would eat all the memory on the
        /// headset while the player waits for nothing at all.
        /// </summary>
        public const int MaxPayloadBytes = 8 * 1024 * 1024;

        public const int SampleRate = 16000;
        public const int Channels = 1;

        /// <summary>Bytes per second of <see cref="Audio"/>: 16-bit mono.</summary>
        public const int BytesPerSecond = SampleRate * Channels * 2;

        public static string Name(byte kind)
        {
            switch (kind)
            {
                case Audio: return "AUDIO";
                case End: return "END";
                case Wav: return "WAV";
                case Text: return "TEXT";
                case Status: return "STATUS";
                default: return $"0x{kind:X2}";
            }
        }

        /// <summary>Packs one frame: type byte, big-endian length, payload.</summary>
        public static byte[] Encode(byte kind, byte[] payload)
        {
            int length = payload != null ? payload.Length : 0;

            var frame = new byte[HeaderBytes + length];
            frame[0] = kind;
            frame[1] = (byte)(length >> 24);
            frame[2] = (byte)(length >> 16);
            frame[3] = (byte)(length >> 8);
            frame[4] = (byte)length;

            if (length > 0) System.Buffer.BlockCopy(payload, 0, frame, HeaderBytes, length);

            return frame;
        }

        /// <summary>Reads the big-endian length out of a five-byte header.</summary>
        public static int ReadLength(IList<byte> header, int offset)
        {
            return (header[offset + 1] << 24)
                 | (header[offset + 2] << 16)
                 | (header[offset + 3] << 8)
                 | header[offset + 4];
        }
    }

    /// <summary>One decoded frame: what it is, and its payload.</summary>
    public struct VoiceFrame
    {
        public byte kind;
        public byte[] payload;

        public int Length => payload != null ? payload.Length : 0;

        public override string ToString() => $"{VoiceLinkProtocol.Name(kind)} ({Length} bytes)";
    }

    /// <summary>
    /// Turns the byte stream from a TCP socket back into frames.
    /// </summary>
    /// <remarks>
    /// TCP is a stream, not a sequence of messages: a frame can arrive split across reads, and two
    /// frames can arrive in one read. So bytes are buffered until a whole header and its payload are
    /// present, and <see cref="Feed"/> returns however many frames that completes — which may be none,
    /// and that is not an error.
    ///
    /// A length that cannot be believed (negative once the top bit is set, or absurdly large) faults
    /// the reader rather than throwing: the caller drops the connection and reconnects, which is
    /// cheaper than trying to resynchronise a stream that has already lost its place.
    /// </remarks>
    public sealed class VoiceFrameReader
    {
        private readonly List<byte> _buffer = new List<byte>(VoiceLinkProtocol.HeaderBytes + 4096);

        /// <summary>True once the stream has gone somewhere it cannot be trusted from.</summary>
        public bool Faulted { get; private set; }

        /// <summary>Bytes waiting for the rest of their frame.</summary>
        public int Buffered => _buffer.Count;

        public List<VoiceFrame> Feed(byte[] data, int count)
        {
            var frames = new List<VoiceFrame>();
            if (Faulted || data == null || count <= 0) return frames;

            for (int i = 0; i < count; i++) _buffer.Add(data[i]);

            int offset = 0;
            while (_buffer.Count - offset >= VoiceLinkProtocol.HeaderBytes)
            {
                int length = VoiceLinkProtocol.ReadLength(_buffer, offset);

                if (length < 0 || length > VoiceLinkProtocol.MaxPayloadBytes)
                {
                    Faulted = true;
                    break;
                }

                if (_buffer.Count - offset - VoiceLinkProtocol.HeaderBytes < length) break;

                byte kind = _buffer[offset];

                var payload = new byte[length];
                if (length > 0) _buffer.CopyTo(offset + VoiceLinkProtocol.HeaderBytes, payload, 0, length);

                frames.Add(new VoiceFrame { kind = kind, payload = payload });

                offset += VoiceLinkProtocol.HeaderBytes + length;
            }

            if (offset > 0) _buffer.RemoveRange(0, offset);

            return frames;
        }
    }
}

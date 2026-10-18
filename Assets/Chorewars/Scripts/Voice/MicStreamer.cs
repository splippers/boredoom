using System.Collections.Generic;
using Chorewars.Diagnostics;
using UnityEngine;

namespace Chorewars.Voice
{
    /// <summary>
    /// Keeps the microphone open and sends the PC only what is said.
    /// </summary>
    /// <remarks>
    /// Privacy: <see cref="Listening"/> is exactly what the red LISTENING dot shows, so what the player sees
    /// is what the code does. Audio is gated by <see cref="UtteranceGate"/>: silence never leaves the headset,
    /// speech goes to the PC's loopback to be turned into text, and nothing is stored on the headset. The
    /// player can mute with the panel's Mute button. While the headset itself is speaking, the mic is ignored
    /// so Claude does not hear Claude (<see cref="Suppressed"/>).
    /// </remarks>
    public sealed class MicStreamer : MonoBehaviour
    {
        public VoiceLinkClient Link;

        /// <summary>Set by the panel while an instruction is being spoken.</summary>
        public bool Suppressed;

        const int TargetRate = VoiceLinkProtocol.SampleRate;
        const int FlushSamples = TargetRate / 20; // judge speech in 50 ms steps

        AudioClip _clip;
        int _lastPos;
        readonly UtteranceGate _gate = new UtteranceGate();
        readonly List<byte> _pending = new List<byte>();

        /// <summary>The microphone is open (not muted).</summary>
        public bool Listening { get; private set; }

        /// <summary>A sentence is going to the PC right now.</summary>
        public bool Hearing => _gate.InUtterance;

        /// <summary>Recent loudness, for the on-screen waveform. Numbers only; no audio is kept.</summary>
        public readonly LevelHistory Levels = new LevelHistory(48);

        public void Begin()
        {
            if (Listening) return;
            if (Microphone.devices.Length == 0)
            {
                TelemetryLogger.Warn("Voice", "no microphone available");
                return;
            }

            _clip = Microphone.Start(null, true, 2, TargetRate);
            _lastPos = 0;
            _pending.Clear();
            Listening = true;
            TelemetryLogger.Info("Voice", "microphone open; clip rate " + _clip.frequency + " Hz");
        }

        public void Stop(string why)
        {
            if (!Listening) return;

            Listening = false;
            Levels.Clear();
            Microphone.End(null);
            Emit(_gate.Stop());
            _pending.Clear();
            TelemetryLogger.Info("Voice", "microphone closed: " + why);
        }

        void Update()
        {
            if (!Listening || _clip == null) return;

            int pos = Microphone.GetPosition(null);
            PcmAudio.RingDelta(_clip.samples, _lastPos, pos, out int firstOffset, out int firstCount, out int secondCount);
            _lastPos = pos;

            if (firstCount > 0) Take(firstOffset, firstCount);
            if (secondCount > 0) Take(0, secondCount);
        }

        void Take(int offset, int count)
        {
            var samples = new float[count];
            _clip.GetData(samples, offset);
            if (_clip.frequency != TargetRate) samples = Resample(samples, _clip.frequency);

            if (Suppressed)
            {
                Emit(_gate.Stop()); // do not record our own voice; end any half sentence
                _pending.Clear();
                Levels.Push(0f);
                return;
            }

            _pending.AddRange(PcmAudio.FloatToS16le(samples, samples.Length));

            while (_pending.Count >= FlushSamples * 2)
            {
                var chunk = _pending.GetRange(0, FlushSamples * 2).ToArray();
                _pending.RemoveRange(0, FlushSamples * 2);
                Levels.Push(PcmAudio.Rms(chunk));
                Emit(_gate.Push(chunk, FlushSamples / (float)TargetRate));
            }
        }

        void Emit(UtteranceGate.Output output)
        {
            foreach (var audio in output.Audio) Link.Send(VoiceLinkProtocol.Audio, audio);
            if (output.End) Link.Send(VoiceLinkProtocol.End, null);
        }

        /// <summary>Nearest-sample conversion; speech to a transcriber does not need better.</summary>
        static float[] Resample(float[] source, int fromRate)
        {
            int length = (int)((long)source.Length * TargetRate / fromRate);
            var result = new float[length];
            for (int i = 0; i < length; i++)
                result[i] = source[(int)((long)i * fromRate / TargetRate)];
            return result;
        }
    }
}

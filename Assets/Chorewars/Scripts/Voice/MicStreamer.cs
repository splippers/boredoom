using Chorewars.Diagnostics;
using UnityEngine;

namespace Chorewars.Voice
{
    /// <summary>
    /// Streams the microphone to the PC, but only between <see cref="Begin"/> and <see cref="Stop"/>.
    /// </summary>
    /// <remarks>
    /// Privacy: the microphone is not opened until the player asks, by looking at the Talk button, and it
    /// is closed again on the player's second look, after a spell of silence, or at the time limit.
    /// <see cref="Recording"/> is what the red "listening" dot shows, so what the player sees is what the
    /// code does. Audio goes to the PC's loopback and is not stored on the headset.
    /// </remarks>
    public sealed class MicStreamer : MonoBehaviour
    {
        public VoiceLinkClient Link;

        const int TargetRate = VoiceLinkProtocol.SampleRate;
        const float MaxSeconds = 20f;
        const float GiveUpIfSilentSeconds = 6f;

        AudioClip _clip;
        int _lastPos;
        float _startedAt;
        bool _heardSpeech;
        readonly EnergyVad _vad = new EnergyVad(0.02f, 1.5f);

        public bool Recording { get; private set; }

        public void Begin()
        {
            if (Recording) return;
            if (Microphone.devices.Length == 0)
            {
                TelemetryLogger.Warn("Voice", "no microphone available");
                return;
            }

            _clip = Microphone.Start(null, true, 2, TargetRate);
            _lastPos = 0;
            _startedAt = Time.unscaledTime;
            _heardSpeech = false;
            _vad.Reset();
            Recording = true;
            TelemetryLogger.Info("Voice", "listening started; clip rate " + _clip.frequency + " Hz");
        }

        public void Stop(string why)
        {
            if (!Recording) return;

            Recording = false;
            Microphone.End(null);
            Link.Send(VoiceLinkProtocol.End, null);
            TelemetryLogger.Info("Voice", "listening stopped: " + why);
        }

        void Update()
        {
            if (!Recording || _clip == null) return;

            int pos = Microphone.GetPosition(null);
            PcmAudio.RingDelta(_clip.samples, _lastPos, pos, out int firstOffset, out int firstCount, out int secondCount);
            _lastPos = pos;

            if (firstCount > 0) Send(firstOffset, firstCount);
            if (secondCount > 0) Send(0, secondCount);

            float elapsed = Time.unscaledTime - _startedAt;
            if (elapsed >= MaxSeconds) Stop("time limit");
            else if (!_heardSpeech && elapsed >= GiveUpIfSilentSeconds) Stop("nothing heard");
        }

        void Send(int offset, int count)
        {
            var samples = new float[count];
            _clip.GetData(samples, offset);

            if (_clip.frequency != TargetRate) samples = Resample(samples, _clip.frequency);

            var pcm = PcmAudio.FloatToS16le(samples, samples.Length);
            Link.Send(VoiceLinkProtocol.Audio, pcm);

            var verdict = _vad.Push(pcm, samples.Length / (float)TargetRate);
            if (verdict == VadEvent.Started) _heardSpeech = true;
            else if (verdict == VadEvent.Ended) Stop("you finished speaking");
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

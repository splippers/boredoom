using System.Collections.Generic;

namespace Chorewars.Voice
{
    /// <summary>
    /// Decides which microphone audio is worth sending when the mic is always open.
    /// </summary>
    /// <remarks>
    /// Only speech leaves the headset. Silence is held in a short pre-roll (so the first syllable is not
    /// clipped), and is thrown away if nothing follows. A sentence is closed when the speaker stops, or at
    /// <see cref="MaxUtteranceSeconds"/> so a long noise (a hoover running) cannot stream forever.
    /// No Unity in here, so the rules are tested without a microphone.
    /// </remarks>
    public sealed class UtteranceGate
    {
        public float PreRollSeconds = 0.4f;
        public float MaxUtteranceSeconds = 30f;

        readonly EnergyVad _vad;
        readonly List<byte[]> _preRoll = new List<byte[]>();
        readonly List<float> _preRollSeconds = new List<float>();
        float _preRollTotal;
        float _utteranceSeconds;

        public UtteranceGate(EnergyVad vad = null)
        {
            _vad = vad ?? new EnergyVad(0.02f, 1.5f);
        }

        /// <summary>True while a sentence is being sent.</summary>
        public bool InUtterance { get; private set; }

        public struct Output
        {
            /// <summary>Audio to send now, in order. Empty when the chunk was silence.</summary>
            public List<byte[]> Audio;

            /// <summary>True when the sentence is over and an END frame should follow the audio.</summary>
            public bool End;
        }

        public Output Push(byte[] pcm, float seconds)
        {
            var output = new Output { Audio = new List<byte[]>(), End = false };
            if (pcm == null || pcm.Length == 0) return output;

            switch (_vad.Push(pcm, seconds))
            {
                case VadEvent.Started:
                    foreach (var held in _preRoll) output.Audio.Add(held);
                    ClearPreRoll();
                    output.Audio.Add(pcm);
                    InUtterance = true;
                    _utteranceSeconds = seconds;
                    break;

                case VadEvent.Speaking:
                    if (!InUtterance) break;
                    output.Audio.Add(pcm);
                    _utteranceSeconds += seconds;
                    if (_utteranceSeconds >= MaxUtteranceSeconds)
                    {
                        output.End = true;
                        Close();
                    }
                    break;

                case VadEvent.Ended:
                    if (InUtterance) output.Audio.Add(pcm);
                    output.End = InUtterance;
                    Close();
                    break;

                default: // Idle: keep a little, drop the rest
                    HoldPreRoll(pcm, seconds);
                    break;
            }

            return output;
        }

        /// <summary>The mic is closing or muted: end any sentence in progress, forget the rest.</summary>
        public Output Stop()
        {
            var output = new Output { Audio = new List<byte[]>(), End = InUtterance };
            Close();
            ClearPreRoll();
            return output;
        }

        void Close()
        {
            InUtterance = false;
            _utteranceSeconds = 0f;
            _vad.Reset();
        }

        void HoldPreRoll(byte[] pcm, float seconds)
        {
            _preRoll.Add(pcm);
            _preRollSeconds.Add(seconds);
            _preRollTotal += seconds;

            while (_preRoll.Count > 1 && _preRollTotal > PreRollSeconds)
            {
                _preRollTotal -= _preRollSeconds[0];
                _preRoll.RemoveAt(0);
                _preRollSeconds.RemoveAt(0);
            }
        }

        void ClearPreRoll()
        {
            _preRoll.Clear();
            _preRollSeconds.Clear();
            _preRollTotal = 0f;
        }
    }
}

using System;

namespace Chorewars.Voice
{
    /// <summary>
    /// The last few moments of microphone loudness, oldest to newest, ready to draw as a scrolling waveform.
    /// </summary>
    /// <remarks>
    /// Loudness is shown on a decibel scale, because a linear one makes ordinary speech a sliver next to a
    /// shout. <see cref="FloorDb"/> maps to an empty bar and <see cref="CeilingDb"/> to a full one. Only
    /// numbers are kept — never audio — so drawing the meter costs the player no privacy.
    /// </remarks>
    public sealed class LevelHistory
    {
        public const float FloorDb = -60f;
        public const float CeilingDb = -15f;

        readonly float[] _rms;
        int _next;
        int _count;

        public LevelHistory(int length)
        {
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
            _rms = new float[length];
        }

        public int Length => _rms.Length;

        /// <summary>How many values have been pushed, up to <see cref="Length"/>.</summary>
        public int Count => _count;

        /// <summary>Adds the newest loudness (RMS, 0 to 1). The oldest value falls off the far end.</summary>
        public void Push(float rms)
        {
            _rms[_next] = float.IsNaN(rms) || rms < 0f ? 0f : Math.Min(rms, 1f);
            _next = (_next + 1) % _rms.Length;
            if (_count < _rms.Length) _count++;
        }

        public void Clear()
        {
            Array.Clear(_rms, 0, _rms.Length);
            _next = 0;
            _count = 0;
        }

        /// <summary>
        /// Bar height 0 to 1 for slot <paramref name="index"/>, where 0 is the oldest slot and
        /// <c>Length - 1</c> the newest. Slots not yet filled read as silence.
        /// </summary>
        public float Height(int index)
        {
            if (index < 0 || index >= _rms.Length) return 0f;

            int back = _rms.Length - 1 - index; // 0 = newest
            if (back >= _count) return 0f;

            int slot = ((_next - 1 - back) % _rms.Length + _rms.Length) % _rms.Length;
            return ToHeight(_rms[slot]);
        }

        public static float ToHeight(float rms)
        {
            if (rms <= 0f) return 0f;

            float db = 20f * (float)Math.Log10(Math.Max(rms, 1e-5f));
            return Math.Max(0f, Math.Min(1f, (db - FloorDb) / (CeilingDb - FloorDb)));
        }
    }
}

using UnityEngine;

namespace Chorewars.Voice
{
    /// <summary>
    /// Turns how loud the voice is into how far the mouth is open, smoothed so it flutters like speech rather
    /// than flickering with every sample.
    /// </summary>
    public static class MouthMotion
    {
        /// <summary>Speech RMS at which the mouth is fully open.</summary>
        public const float LoudRms = 0.12f;

        public static float Next(float current, float rms, float dt)
        {
            float target = Mathf.Clamp01(rms / LoudRms);
            float rate = target > current ? 28f : 12f;   // opens fast, closes a little slower
            return Mathf.MoveTowards(current, target, rate * dt);
        }
    }
}

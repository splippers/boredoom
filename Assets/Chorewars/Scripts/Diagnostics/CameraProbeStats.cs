using System.Collections.Generic;

namespace Chorewars.Diagnostics
{
    /// <summary>
    /// Counts camera frames and turns them into a rate. Pure logic, so it is tested without a headset.
    /// </summary>
    /// <remarks>
    /// Only times are recorded — never pixels — which keeps the probe's whole footprint to a number.
    /// </remarks>
    public sealed class CameraProbeStats
    {
        readonly List<double> _times = new List<double>();

        public int Frames => _times.Count;

        public void Record(double seconds) => _times.Add(seconds);

        /// <summary>Frames per second across the recorded span; 0 until two frames at different times exist.</summary>
        public double FramesPerSecond
        {
            get
            {
                if (_times.Count < 2) return 0;

                double span = _times[_times.Count - 1] - _times[0];
                return span <= 0 ? 0 : (_times.Count - 1) / span;
            }
        }

        /// <summary>One readable line for the log.</summary>
        public string Describe(int width, int height, string format)
        {
            if (Frames == 0) return "no camera frames received";

            return Frames + " frames, " + width + "x" + height + " " + format + ", " +
                   FramesPerSecond.ToString("0.0") + " fps";
        }
    }
}

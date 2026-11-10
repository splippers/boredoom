using System;
using System.Collections.Generic;

namespace Chorewars.Weapon
{
    /// <summary>
    /// "Choose your weapon!": learns what an appliance (the hoover) sounds like running versus quiet, then tells running from not running.
    /// Pure logic (no Unity audio), so it is unit tested. It says WHETHER the machine runs, never WHERE it is.
    /// </summary>
    public sealed class ApplianceSoundProfile
    {
        public const int Bands = 7;
        public const int FrameSize = 1024;
        static readonly float[] Edges = { 100f, 200f, 400f, 800f, 1600f, 3200f, 6400f, 12800f };
        readonly List<float[]> quiet = new List<float[]>(), running = new List<float[]>();
        float[] quietMean, runningMean;

        public int QuietFrames => quiet.Count;
        public int RunningFrames => running.Count;
        /// <summary>Distance in dB-bands between the learned quiet and running sounds; bigger is easier to tell apart.</summary>
        public float Separation { get; private set; }
        public bool IsReady { get; private set; }

        /// <summary>Band energies in dB for one frame of mono samples (first FrameSize used, zero-padded if shorter).</summary>
        public static float[] Features(float[] samples, int count, int sampleRate)
        {
            var re = new float[FrameSize]; var im = new float[FrameSize];
            int n = Math.Min(count, FrameSize);
            for (int i = 0; i < n; i++) re[i] = samples[i] * (0.5f - 0.5f * (float)Math.Cos(2 * Math.PI * i / (FrameSize - 1)));
            Fft(re, im);
            var f = new float[Bands];
            var energy = new double[Bands];
            for (int k = 1; k < FrameSize / 2; k++)
            {
                float hz = k * (float)sampleRate / FrameSize;
                for (int b = 0; b < Bands; b++)
                    if (hz >= Edges[b] && hz < Edges[b + 1]) { energy[b] += re[k] * re[k] + im[k] * im[k]; break; }
            }
            for (int b = 0; b < Bands; b++) f[b] = 10f * (float)Math.Log10(energy[b] / FrameSize + 1e-9);
            return f;
        }

        public void AddQuiet(float[] features) { quiet.Add(features); IsReady = false; }
        public void AddRunning(float[] features) { running.Add(features); IsReady = false; }
        public void Reset() { quiet.Clear(); running.Clear(); IsReady = false; Separation = 0f; }

        /// <summary>Finish learning. False with a reason if there is too little data or the two sounds are too alike.</summary>
        public bool TryFinish(out string problem)
        {
            problem = null;
            if (quiet.Count < 20 || running.Count < 20) { problem = "Need a few more seconds of both quiet and running sound."; return false; }
            quietMean = Mean(quiet); runningMean = Mean(running);
            Separation = Distance(quietMean, runningMean);
            if (Separation < 8f) { problem = "The running and quiet sounds are too alike. Move closer to the hoover or reduce other noise."; return false; }
            IsReady = true; return true;
        }

        /// <summary>Positive when the frame sounds more like the running machine; magnitude is in the same units as Separation.</summary>
        public float Score(float[] features)
        {
            if (!IsReady) return 0f;
            return Distance(features, quietMean) - Distance(features, runningMean);
        }

        public float Separate => Separation;

        static float[] Mean(List<float[]> list)
        {
            var m = new float[Bands];
            foreach (var f in list) for (int i = 0; i < Bands; i++) m[i] += f[i];
            for (int i = 0; i < Bands; i++) m[i] /= list.Count;
            return m;
        }

        static float Distance(float[] a, float[] b)
        {
            double s = 0; for (int i = 0; i < Bands; i++) { double d = a[i] - b[i]; s += d * d; }
            return (float)Math.Sqrt(s);
        }

        static void Fft(float[] re, float[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len; float wr = (float)Math.Cos(ang), wi = (float)Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    float cr = 1f, ci = 0f;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = i + k + len / 2;
                        float tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr; im[b] = im[a] - ti; re[a] += tr; im[a] += ti;
                        float ncr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = ncr;
                    }
                }
            }
        }
    }

    /// <summary>Turns a stream of profile scores into a steady Running/Stopped state, with debounce and a running-time total.</summary>
    public sealed class ApplianceActivity
    {
        readonly float onSeconds, offSeconds, threshold;
        float held;
        public bool Running { get; private set; }
        public float ActiveSeconds { get; private set; }
        public event Action<bool> Changed;

        /// <param name="threshold">Score magnitude needed (use about a quarter of the profile Separation).</param>
        public ApplianceActivity(float threshold, float onSeconds = 0.6f, float offSeconds = 1.2f)
        { this.threshold = threshold; this.onSeconds = onSeconds; this.offSeconds = offSeconds; }

        public void Update(float score, float dt)
        {
            if (Running) ActiveSeconds += dt;
            bool wantsRunning = score > threshold, wantsStopped = score < -threshold;
            if (!Running && wantsRunning) { held += dt; if (held >= onSeconds) { Running = true; held = 0f; Changed?.Invoke(true); } }
            else if (Running && wantsStopped) { held += dt; if (held >= offSeconds) { Running = false; held = 0f; Changed?.Invoke(false); } }
            else held = 0f;
        }
    }
}

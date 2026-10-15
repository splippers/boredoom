using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Chorewars.Diagnostics
{
    /// <summary>Summary of one named series of numbers.</summary>
    public struct SampleStats
    {
        public int Count;
        public float Last;
        public float Min;
        public float Max;
        public float Mean;

        /// <summary>Median of the recent samples held in memory, not of the whole run.</summary>
        public float P50;

        /// <summary>95th centile of the recent samples held in memory.</summary>
        public float P95;

        /// <summary>99th centile of the recent samples held in memory.</summary>
        public float P99;
    }

    /// <summary>
    /// Named series of numbers, held in memory and reported as statistics.
    /// </summary>
    /// <remarks>
    /// A line per frame is not telemetry, it is a second framerate problem: measuring frame time by
    /// writing a line per frame measures the logger. So values are accumulated here and reported once,
    /// as counts and centiles.
    ///
    /// Percentiles come from a bounded ring of the most recent samples per series, not from the whole
    /// run, so the memory cost is fixed whatever the run length. That means p95 answers "what is bad
    /// like, lately" rather than "what was the worst second of the run"; the lifetime min and max
    /// answer that. The count is lifetime, so a series that stopped feeding is visible as a count that
    /// does not grow.
    /// </remarks>
    public static class TelemetrySamples
    {
        /// <summary>Samples kept per series for the centiles. Fixed, whatever the run length.</summary>
        public const int Window = 4096;

        sealed class Series
        {
            public int Count;

            /// <summary>
            /// Values that can be added up. Not the same as <see cref="Count"/>: a NaN that arrived
            /// is worth counting and would poison every sum, so the two are kept apart.
            /// </summary>
            public int Numeric;

            public float Sum;
            public float Min = float.MaxValue;
            public float Max = float.MinValue;
            public float Last;
            public readonly float[] Ring = new float[Window];
            public int Filled;
            public int Head;
        }

        static readonly Dictionary<string, Series> SeriesByName = new Dictionary<string, Series>(32, StringComparer.Ordinal);
        static readonly List<string> NamesInOrder = new List<string>(32);

        /// <summary>Series names, in the order they were first seen.</summary>
        public static IReadOnlyList<string> Names => NamesInOrder;

        /// <summary>Records one value, or nothing at all if the series cannot be named.</summary>
        public static void Add(string name, float value)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (!SeriesByName.TryGetValue(name, out var series))
            {
                series = new Series();
                SeriesByName[name] = series;
                NamesInOrder.Add(name);
            }

            series.Count++;
            series.Last = value;

            // NaN poisons min/max/sum for the rest of the run, and one bad reading is exactly what the
            // report needs to be able to name. It is counted and kept as the last value, and left out
            // of the arithmetic.
            if (float.IsNaN(value) || float.IsInfinity(value)) return;

            series.Numeric++;
            series.Sum += value;
            if (value < series.Min) series.Min = value;
            if (value > series.Max) series.Max = value;

            series.Ring[series.Head] = value;
            series.Head = (series.Head + 1) % Window;
            if (series.Filled < Window) series.Filled++;
        }

        public static bool TryGet(string name, out SampleStats stats)
        {
            stats = default;

            if (string.IsNullOrEmpty(name) || !SeriesByName.TryGetValue(name, out var series)) return false;

            stats = Summarise(series);
            return true;
        }

        /// <summary>Everything about <paramref name="name"/>, zeroed if it was never fed.</summary>
        public static SampleStats Get(string name)
            => !string.IsNullOrEmpty(name) && SeriesByName.TryGetValue(name, out var series)
                ? Summarise(series)
                : default;

        public static int CountOf(string name)
            => !string.IsNullOrEmpty(name) && SeriesByName.TryGetValue(name, out var series) ? series.Count : 0;

        public static void Clear()
        {
            SeriesByName.Clear();
            NamesInOrder.Clear();
        }

        /// <summary>
        /// One JSON line holding every series. Built by hand rather than with JsonUtility, which cannot
        /// serialise a dictionary; series names are identifiers this code chose, and are sanitised
        /// anyway.
        /// </summary>
        public static string Report()
        {
            var sb = new StringBuilder(256 + NamesInOrder.Count * 160);

            sb.Append("{\"kind\":\"samples\",\"ts\":\"").Append(DateTime.UtcNow.ToString("o")).Append('"');
            sb.Append(",\"window\":").Append(Window);
            sb.Append(",\"series\":[");

            for (int i = 0; i < NamesInOrder.Count; i++)
            {
                var name = NamesInOrder[i];
                var s = Summarise(SeriesByName[name]);

                if (i > 0) sb.Append(',');
                sb.Append("{\"n\":\"").Append(Sanitise(name)).Append('"');
                sb.Append(",\"count\":").Append(s.Count);
                sb.Append(",\"min\":").Append(Number(s.Min));
                sb.Append(",\"max\":").Append(Number(s.Max));
                sb.Append(",\"mean\":").Append(Number(s.Mean));
                sb.Append(",\"p50\":").Append(Number(s.P50));
                sb.Append(",\"p95\":").Append(Number(s.P95));
                sb.Append(",\"p99\":").Append(Number(s.P99));
                sb.Append(",\"last\":").Append(Number(s.Last));
                sb.Append('}');
            }

            sb.Append("]}");
            return sb.ToString();
        }

        static SampleStats Summarise(Series series)
        {
            var stats = new SampleStats
            {
                Count = series.Count,
                Last = series.Last,
                Min = series.Filled == 0 ? 0f : series.Min,
                Max = series.Filled == 0 ? 0f : series.Max,
                Mean = series.Numeric == 0 ? 0f : series.Sum / series.Numeric,
            };

            if (series.Filled == 0) return stats;

            var sorted = new float[series.Filled];
            Array.Copy(series.Ring, sorted, series.Filled);
            Array.Sort(sorted);

            stats.P50 = Centile(sorted, 0.50f);
            stats.P95 = Centile(sorted, 0.95f);
            stats.P99 = Centile(sorted, 0.99f);

            return stats;
        }

        static float Centile(float[] sorted, float fraction)
        {
            if (sorted.Length == 0) return 0f;

            int index = Mathf.Clamp(Mathf.FloorToInt(fraction * (sorted.Length - 1)), 0, sorted.Length - 1);
            return sorted[index];
        }

        static string Number(float value)
            => float.IsNaN(value) ? "\"NaN\""
                : float.IsInfinity(value) ? (value > 0 ? "\"Infinity\"" : "\"-Infinity\"")
                : value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        static string Sanitise(string name)
        {
            var sb = new StringBuilder(name.Length);

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' ? c : '_');
            }

            return sb.ToString();
        }
    }
}

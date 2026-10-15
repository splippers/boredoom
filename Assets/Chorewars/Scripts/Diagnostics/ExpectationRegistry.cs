using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Chorewars.Diagnostics
{
    /// <summary>How a component check came out.</summary>
    public enum ExpectationVerdict
    {
        /// <summary>Nothing was reported, so there is nothing to say. Not a pass.</summary>
        Unknown,

        /// <summary>Inside the range it has to be in.</summary>
        Ok,

        /// <summary>Outside the range it has to be in, but not absurd: the component is broken.</summary>
        Broken,

        /// <summary>
        /// Off by an order of magnitude, or not a number, or zero where zero is impossible. The value
        /// is not merely out of tolerance, it did not come from the thing being measured.
        /// </summary>
        WayOff,
    }

    /// <summary>An inclusive range, with the two facts a check needs about it.</summary>
    [Serializable]
    public struct ExpectationRange
    {
        public float Min;
        public float Max;

        public static ExpectationRange Between(float min, float max) => new ExpectationRange { Min = min, Max = max };

        public static ExpectationRange AtLeast(float min) => new ExpectationRange { Min = min, Max = float.MaxValue };

        public static ExpectationRange AtMost(float max) => new ExpectationRange { Min = float.MinValue, Max = max };

        public bool Contains(float value) => value >= Min && value <= Max;

        /// <summary>
        /// Whether a zero here would be impossible. A count of rooms, a framerate, a rate per second:
        /// all have a floor above zero, and a reading of exactly zero means the number never arrived
        /// rather than that the room count is low.
        /// </summary>
        public bool ExcludesZero => Min > 0f || Max < 0f;
    }

    /// <summary>
    /// What each component was expected to do, and what it actually did.
    /// </summary>
    /// <remarks>
    /// The point of a slice build is to answer "is this component working" without a person reading
    /// logs, so each component states its expectation up front and reports a value. The expectation is
    /// declared where the value is produced, which keeps the range next to the thing that knows what a
    /// plausible number looks like — a mesh count of 40 is right on a Quest and absurd in the editor.
    ///
    /// Three verdicts, not a score. A value inside its range is Ok. A value outside the range but
    /// still recognisably a measurement is Broken: the component ran and produced something wrong. A
    /// value that is NaN, infinite, zero when zero is impossible, or outside the plausible range is
    /// WayOff: the number did not come from the component at all. Broken and WayOff send you to
    /// different places, so they are not the same verdict.
    /// </remarks>
    public static class ExpectationRegistry
    {
        sealed class Expectation
        {
            public string Name;
            public string Unit;
            public string Note;
            public ExpectationRange Ok;
            public ExpectationRange Plausible;
            public float Value;
            public bool HasValue;
        }

        static readonly Dictionary<string, Expectation> ByName = new Dictionary<string, Expectation>(32, StringComparer.Ordinal);
        static readonly List<string> NamesInOrder = new List<string>(32);

        /// <summary>Names in the order they were first defined.</summary>
        public static IReadOnlyList<string> Names => NamesInOrder;

        /// <summary>
        /// Declares what a component must do and what it may plausibly do, then reports the value.
        /// The one call a component needs.
        /// </summary>
        public static void Expect(string name, float value, ExpectationRange ok, ExpectationRange plausible,
            string unit = null, string note = null)
        {
            Define(name, ok, plausible, unit, note);
            Report(name, value);
        }

        /// <summary>Declares the ranges without a value yet.</summary>
        public static void Define(string name, ExpectationRange ok, ExpectationRange plausible,
            string unit = null, string note = null)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (!ByName.TryGetValue(name, out var expectation))
            {
                expectation = new Expectation { Name = name };
                ByName[name] = expectation;
                NamesInOrder.Add(name);
            }

            expectation.Ok = ok;
            expectation.Plausible = plausible;
            expectation.Unit = unit;
            expectation.Note = note;
        }

        /// <summary>Reports a value into an expectation declared earlier.</summary>
        public static void Report(string name, float value)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (!ByName.TryGetValue(name, out var expectation))
            {
                // An undeclared check is worth knowing about, and must not be silently ignored: the
                // alternative is a component that reports into nothing and looks healthy.
                expectation = new Expectation
                {
                    Name = name,
                    Ok = ExpectationRange.Between(float.MinValue, float.MaxValue),
                    Plausible = ExpectationRange.Between(float.MinValue, float.MaxValue),
                    Note = "no expectation declared",
                };
                ByName[name] = expectation;
                NamesInOrder.Add(name);
            }

            expectation.Value = value;
            expectation.HasValue = true;
        }

        /// <summary>
        /// Reports a value from a series instead of a single reading, so a component that only makes
        /// sense across a run — frames per second, frames of age — can be judged without the caller
        /// deciding which statistic matters.
        /// </summary>
        public static void ReportSamples(string name, SampleStatistic which = SampleStatistic.P95)
            => ReportSamples(name, name, which);

        /// <summary>
        /// Reports from a series whose name differs from the check's. Worth spelling out rather than
        /// renaming one to match the other: a check reads as "photoRate" while the series it is drawn
        /// from reads as "photoPerSec", and forcing those to be equal makes both worse.
        /// </summary>
        public static void ReportSamples(string seriesName, string checkName, SampleStatistic which)
        {
            if (!TelemetrySamples.TryGet(seriesName, out var stats))
            {
                // A check bound to a series that never reported is a wiring mistake until proven
                // otherwise, and it would otherwise sit at UNKNOWN forever looking like a slow start.
                // Said once per pair, not once per call, because this runs on the HUD's tick too.
                var key = seriesName + "\\u0000" + checkName;
                if (WarnedPairs.Add(key))
                    TelemetryLogger.Warn("telemetry", $"check '{checkName}' has no samples in series '{seriesName}'");

                return;
            }

            Report(checkName, Select(stats, which));
        }

        static readonly HashSet<string> WarnedPairs = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>What a series does, as opposed to what it did once.</summary>
        public enum SampleStatistic
        {
            Last,
            Mean,
            Min,
            Max,
            P50,
            P95,
            P99,
        }

        static float Select(SampleStats stats, SampleStatistic which)
        {
            switch (which)
            {
                case SampleStatistic.Mean: return stats.Mean;
                case SampleStatistic.Min: return stats.Min;
                case SampleStatistic.Max: return stats.Max;
                case SampleStatistic.P50: return stats.P50;
                case SampleStatistic.P95: return stats.P95;
                case SampleStatistic.P99: return stats.P99;
                default: return stats.Last;
            }
        }

        public static ExpectationVerdict VerdictOf(string name)
            => !string.IsNullOrEmpty(name) && ByName.TryGetValue(name, out var expectation)
                ? Judge(expectation)
                : ExpectationVerdict.Unknown;

        public static int CountOf(ExpectationVerdict verdict)
        {
            int count = 0;

            for (int i = 0; i < NamesInOrder.Count; i++)
            {
                if (Judge(ByName[NamesInOrder[i]]) == verdict) count++;
            }

            return count;
        }

        public static void Clear()
        {
            ByName.Clear();
            NamesInOrder.Clear();
            WarnedPairs.Clear();
        }

        static ExpectationVerdict Judge(Expectation expectation)
        {
            if (!expectation.HasValue) return ExpectationVerdict.Unknown;

            float value = expectation.Value;

            if (float.IsNaN(value) || float.IsInfinity(value)) return ExpectationVerdict.WayOff;
            if (expectation.Ok.ExcludesZero && value == 0f) return ExpectationVerdict.WayOff;
            if (!expectation.Plausible.Contains(value)) return ExpectationVerdict.WayOff;
            if (!expectation.Ok.Contains(value)) return ExpectationVerdict.Broken;

            return ExpectationVerdict.Ok;
        }

        /// <summary>Short human line: "scan OK, camera BROKEN (18 of 30 fps), coverage OK".</summary>
        public static string Summary()
        {
            var sb = new StringBuilder();

            for (int i = 0; i < NamesInOrder.Count; i++)
            {
                var expectation = ByName[NamesInOrder[i]];
                var verdict = Judge(expectation);

                if (sb.Length > 0) sb.Append(", ");
                sb.Append(expectation.Name).Append(' ').Append(Label(verdict));

                if (verdict != ExpectationVerdict.Ok && verdict != ExpectationVerdict.Unknown && expectation.HasValue)
                    sb.Append(" (").Append(expectation.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                if (verdict != ExpectationVerdict.Ok && verdict != ExpectationVerdict.Unknown && !string.IsNullOrEmpty(expectation.Unit))
                    sb.Append(' ').Append(expectation.Unit);
                if (verdict != ExpectationVerdict.Ok && verdict != ExpectationVerdict.Unknown)
                    sb.Append(')');
            }

            return sb.ToString();
        }

        public static string Label(ExpectationVerdict verdict)
        {
            switch (verdict)
            {
                case ExpectationVerdict.Ok: return "OK";
                case ExpectationVerdict.Broken: return "BROKEN";
                case ExpectationVerdict.WayOff: return "WAY_OFF";
                default: return "UNKNOWN";
            }
        }

        /// <summary>One JSON line holding every check, its value, its ranges and its verdict.</summary>
        public static string Report()
        {
            var sb = new StringBuilder(256 + NamesInOrder.Count * 220);

            sb.Append("{\"kind\":\"expectations\",\"ts\":\"").Append(DateTime.UtcNow.ToString("o")).Append('"');
            sb.Append(",\"ok\":").Append(CountOf(ExpectationVerdict.Ok));
            sb.Append(",\"broken\":").Append(CountOf(ExpectationVerdict.Broken));
            sb.Append(",\"wayoff\":").Append(CountOf(ExpectationVerdict.WayOff));
            sb.Append(",\"unknown\":").Append(CountOf(ExpectationVerdict.Unknown));
            sb.Append(",\"summary\":\"").Append(Escape(Summary())).Append('"');
            sb.Append(",\"checks\":[");

            for (int i = 0; i < NamesInOrder.Count; i++)
            {
                var expectation = ByName[NamesInOrder[i]];

                if (i > 0) sb.Append(',');
                sb.Append("{\"n\":\"").Append(Escape(expectation.Name)).Append('"');
                sb.Append(",\"verdict\":\"").Append(Label(Judge(expectation))).Append('"');
                if (expectation.HasValue) sb.Append(",\"value\":").Append(Number(expectation.Value));
                sb.Append(",\"ok\":[").Append(Number(expectation.Ok.Min)).Append(',').Append(Number(expectation.Ok.Max)).Append(']');
                sb.Append(",\"plausible\":[").Append(Number(expectation.Plausible.Min)).Append(',').Append(Number(expectation.Plausible.Max)).Append(']');
                if (!string.IsNullOrEmpty(expectation.Unit)) sb.Append(",\"unit\":\"").Append(Escape(expectation.Unit)).Append('"');
                if (!string.IsNullOrEmpty(expectation.Note)) sb.Append(",\"note\":\"").Append(Escape(expectation.Note)).Append('"');
                sb.Append('}');
            }

            sb.Append("]}");
            return sb.ToString();
        }

        static string Number(float value)
            => float.IsNaN(value) ? "\"NaN\""
                : float.IsInfinity(value) ? (value > 0 ? "\"Infinity\"" : "\"-Infinity\"")
                : value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var sb = new StringBuilder(text.Length + 8);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < ' ') sb.Append(' ');
                else sb.Append(c);
            }

            return sb.ToString();
        }
    }
}

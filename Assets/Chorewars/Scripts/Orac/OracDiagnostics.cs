using System.Collections.Generic;

namespace Chorewars.Orac
{
    /// <summary>The four verdicts ORAC is allowed to give, per the #49 doctrine.</summary>
    public enum OracVerdict
    {
        Unknown = 0,
        Working = 1,
        Broken = 2,
        WayOff = 3
    }

    /// <summary>How much he cares. He pretends not to care at all.</summary>
    public enum OracSeverity
    {
        Trivial = 0,
        Minor = 1,
        Notable = 2,
        Serious = 3,
        Critical = 4
    }

    /// <summary>
    /// One observation about the inner workings of the headset, the build, or the game.
    /// Evidence is machine-readable enough to be logged; the suggestion is what ORAC
    /// thinks you should do about it.
    /// </summary>
    [System.Serializable]
    public class OracFinding
    {
        public string Component;    // e.g. "perf.frame_time"
        public OracVerdict Verdict;
        public OracSeverity Severity;
        public float Value;
        public float Threshold;
        public string Unit;         // "ms", "MB/s", "%"
        public string Evidence;
        public string Suggestion;

        public bool IsProblem
        {
            get { return Verdict == OracVerdict.Broken || Verdict == OracVerdict.WayOff; }
        }

        public string EvidenceLine()
        {
            if (string.IsNullOrEmpty(Unit)) return Evidence;
            return Evidence + " (" + Value.ToString("0.##") + Unit +
                   " against a line of " + Threshold.ToString("0.##") + Unit + ")";
        }

        public static OracFinding Ok(string component, string evidence)
        {
            return new OracFinding
            {
                Component = component,
                Verdict = OracVerdict.Working,
                Severity = OracSeverity.Trivial,
                Evidence = evidence
            };
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.Orac
{
    /// <summary>
    /// ORAC's memory of what he has already told you. He does not like repeating himself,
    /// and he will remind you that he mentioned it an hour ago.
    /// </summary>
    public class OracJournal
    {
        public struct Entry
        {
            public string Component;
            public OracVerdict Verdict;
            public float Time;
            public string Evidence;
        }

        readonly List<Entry> _entries = new List<Entry>();
        readonly int _capacity;

        public OracJournal(int capacity = 256)
        {
            _capacity = Mathf.Max(8, capacity);
        }

        public IList<Entry> Entries { get { return _entries; } }

        public void Record(OracFinding finding, float now)
        {
            if (finding == null) return;
            _entries.Add(new Entry
            {
                Component = finding.Component,
                Verdict = finding.Verdict,
                Time = now,
                Evidence = finding.Evidence
            });
            while (_entries.Count > _capacity) _entries.RemoveAt(0);
        }

        public int Recurrences(string component)
        {
            int n = 0;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Component == component) n++;
            return n;
        }

        public float LastSeen(string component)
        {
            float t = -1f;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Component == component && _entries[i].Time > t) t = _entries[i].Time;
            return t;
        }

        /// <summary>Newest distinct problems, most recent first.</summary>
        public List<string> RecentProblems(int max)
        {
            var seen = new List<string>();
            var output = new List<string>();
            for (int i = _entries.Count - 1; i >= 0 && output.Count < max; i--)
            {
                var e = _entries[i];
                if (e.Verdict != OracVerdict.Broken && e.Verdict != OracVerdict.WayOff) continue;
                if (seen.Contains(e.Component)) continue;
                seen.Add(e.Component);
                output.Add(e.Component + " (" + e.Verdict + "): " + e.Evidence);
            }
            return output;
        }
    }
}

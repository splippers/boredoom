using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chorewars.Orac
{
    /// <summary>
    /// ORAC as virtual SysMedic. He watches the headset's own vitals and reports what is
    /// wrong with them. Strictly read-only: he diagnoses, you fix - or he says so.
    ///
    /// Sampled at roughly 1 Hz. Findings are ranked and journalled so he can be withering
    /// about recurrence as well as magnitude.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class OracSysMedic : MonoBehaviour
    {
        public bool Enabled = true;
        public float SampleInterval = 1f;

        /// <summary>Frame budget in ms. 72 Hz = 13.9 ms; 90 Hz = 11.1 ms.</summary>
        public float FrameBudgetMs = 13.9f;

        /// <summary>Managed allocations above this rate are a leak in ORAC's view.</summary>
        public float AllocBudgetMbPerSec = 1.5f;

        public event System.Action<OracFinding> FindingRaised;

        public OracJournal Journal { get; private set; }
        public OracFinding Latest { get; private set; }

        readonly List<OracFinding> _current = new List<OracFinding>();
        public IList<OracFinding> Current { get { return _current; } }

        float _nextSample;
        float _frameAccum;
        float _frameWorst;
        int _frames;
        long _lastMono;
        int _errors;
        int _exceptions;

        void Awake()
        {
            Journal = new OracJournal();
            Application.logMessageReceived += OnLog;
            _lastMono = Profiler.GetMonoUsedSizeLong();
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error) _errors++;
            else if (type == LogType.Exception || type == LogType.Assert) _exceptions++;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _frameAccum += dt;
            if (dt > _frameWorst) _frameWorst = dt;
            _frames++;

            if (!Enabled) return;
            if (Time.unscaledTime < _nextSample) return;
            _nextSample = Time.unscaledTime + Mathf.Max(0.25f, SampleInterval);
            Sample();
        }

        void Sample()
        {
            _current.Clear();
            SampleFrameTime();
            SampleMemory();
            SampleBattery();
            SampleThermal();
            SampleLogTraffic();
            SampleAppState();

            for (int i = 0; i < _current.Count; i++)
            {
                var f = _current[i];
                if (!f.IsProblem) continue;
                Journal.Record(f, Time.unscaledTime);
                Latest = f;
                if (FindingRaised != null) FindingRaised(f);
            }
        }

        void SampleFrameTime()
        {
            if (_frames == 0) return;
            float avgMs = (_frameAccum / _frames) * 1000f;
            float worstMs = _frameWorst * 1000f;

            OracVerdict verdict;
            if (avgMs <= FrameBudgetMs) verdict = OracVerdict.Working;
            else if (avgMs <= FrameBudgetMs * 1.5f) verdict = OracVerdict.Broken;
            else verdict = OracVerdict.WayOff;

            _current.Add(new OracFinding
            {
                Component = "perf.frame_time",
                Verdict = verdict,
                Severity = verdict == OracVerdict.Working ? OracSeverity.Trivial
                         : verdict == OracVerdict.Broken ? OracSeverity.Notable
                         : OracSeverity.Serious,
                Value = avgMs,
                Threshold = FrameBudgetMs,
                Unit = "ms",
                Evidence = "mean frame " + avgMs.ToString("0.0") + " ms, worst " +
                           worstMs.ToString("0.0") + " ms over " + _frames + " frames",
                Suggestion = "cut draw calls or slow the light chase; the budget is " +
                             FrameBudgetMs.ToString("0.0") + " ms"
            });

            _frameAccum = 0f;
            _frameWorst = 0f;
            _frames = 0;
        }

        void SampleMemory()
        {
            const double Mb = 1024.0 * 1024.0;
            long mono = Profiler.GetMonoUsedSizeLong();
            long total = Profiler.GetTotalAllocatedMemoryLong();
            long reserved = Profiler.GetTotalReservedMemoryLong();

            double growthMb = (mono - _lastMono) / Mb;
            _lastMono = mono;
            double rateMbPerSec = growthMb / Mathf.Max(0.25f, SampleInterval);

            OracVerdict verdict;
            if (rateMbPerSec <= AllocBudgetMbPerSec) verdict = OracVerdict.Working;
            else if (rateMbPerSec <= AllocBudgetMbPerSec * 3.0) verdict = OracVerdict.Broken;
            else verdict = OracVerdict.WayOff;

            _current.Add(new OracFinding
            {
                Component = "perf.alloc_rate",
                Verdict = verdict,
                Severity = verdict == OracVerdict.Working ? OracSeverity.Trivial
                         : verdict == OracVerdict.Broken ? OracSeverity.Notable
                         : OracSeverity.Serious,
                Value = (float)rateMbPerSec,
                Threshold = AllocBudgetMbPerSec,
                Unit = "MB/s",
                Evidence = "managed heap growing " + rateMbPerSec.ToString("0.00") +
                           " MB/s; total " + (total / Mb).ToString("0.0") + " MB of " +
                           (reserved / Mb).ToString("0.0") + " MB reserved",
                Suggestion = "stop allocating in update loops; a collector pause is a frame you will never get back"
            });
        }

        void SampleBattery()
        {
            float level = SystemInfo.batteryLevel;   // 0..1, -1 when unknown
            if (level < 0f) return;

            var status = SystemInfo.batteryStatus;
            float pct = level * 100f;

            OracVerdict verdict = OracVerdict.Working;
            if (status == BatteryStatus.Discharging && pct < 15f) verdict = OracVerdict.WayOff;
            else if (status == BatteryStatus.Discharging && pct < 30f) verdict = OracVerdict.Broken;

            _current.Add(new OracFinding
            {
                Component = "system.battery",
                Verdict = verdict,
                Severity = verdict == OracVerdict.Working ? OracSeverity.Trivial
                         : verdict == OracVerdict.Broken ? OracSeverity.Minor
                         : OracSeverity.Notable,
                Value = pct,
                Threshold = 30f,
                Unit = "%",
                Evidence = "battery " + pct.ToString("0") + "%, status " + status,
                Suggestion = "plug it in; a dead headset is a very poor test rig"
            });
        }

        void SampleThermal()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            int status = -1;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var power = activity.Call<AndroidJavaObject>("getSystemService", "power"))
                {
                    if (power != null) status = power.Call<int>("getCurrentThermalStatus");
                }
            }
            catch
            {
                status = -1;
            }

            if (status < 0) return;

            OracVerdict verdict;
            OracSeverity severity;
            if (status <= 1) { verdict = OracVerdict.Working; severity = OracSeverity.Trivial; }
            else if (status <= 3) { verdict = OracVerdict.Broken; severity = OracSeverity.Notable; }
            else { verdict = OracVerdict.WayOff; severity = OracSeverity.Critical; }

            _current.Add(new OracFinding
            {
                Component = "system.thermal",
                Verdict = verdict,
                Severity = severity,
                Value = status,
                Threshold = 3f,
                Unit = "",
                Evidence = "thermal status " + ThermalName(status) + " (" + status + "/6)",
                Suggestion = status >= 4
                    ? "you are cooking it. Lower the refresh rate, kill MSAA, and let the light chase idle"
                    : "watch the thermals; throttling is the silent killer of frame budgets"
            });
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static string ThermalName(int status)
        {
            switch (status)
            {
                case 0: return "none";
                case 1: return "light";
                case 2: return "moderate";
                case 3: return "severe";
                case 4: return "critical";
                case 5: return "emergency";
                case 6: return "shutdown";
                default: return "unknown";
            }
        }
#endif

        void SampleLogTraffic()
        {
            int errors = _errors;
            int exceptions = _exceptions;
            _errors = 0;
            _exceptions = 0;
            int total = errors + exceptions;

            OracVerdict verdict;
            if (total == 0) verdict = OracVerdict.Working;
            else if (total <= 3) verdict = OracVerdict.Broken;
            else verdict = OracVerdict.WayOff;

            _current.Add(new OracFinding
            {
                Component = "diag.log_traffic",
                Verdict = verdict,
                Severity = verdict == OracVerdict.Working ? OracSeverity.Trivial
                         : verdict == OracVerdict.Broken ? OracSeverity.Notable
                         : OracSeverity.Serious,
                Value = total,
                Threshold = 3f,
                Unit = "",
                Evidence = errors + " errors and " + exceptions + " exceptions since the last sample",
                Suggestion = "read the log. You are producing noise faster than progress"
            });
        }

        void SampleAppState()
        {
            _current.Add(OracFinding.Ok("app.target_fps",
                "targetFrameRate=" + Application.targetFrameRate +
                ", vSync=" + QualitySettings.vSyncCount +
                ", device=" + SystemInfo.deviceModel +
                ", gpu=" + SystemInfo.graphicsDeviceName));
        }

        /// <summary>Let the telemetry layer push its own verdicts in, so ORAC can comment on them.</summary>
        public void AddExternalFinding(OracFinding finding)
        {
            if (finding == null) return;
            _current.Add(finding);
            if (finding.IsProblem)
            {
                Journal.Record(finding, Time.unscaledTime);
                Latest = finding;
                if (FindingRaised != null) FindingRaised(finding);
            }
        }

        public string ComposeReport()
        {
            var sb = new StringBuilder();
            sb.Append("ORAC SysMedic @ ").Append(Time.unscaledTime.ToString("0.0")).Append("s\n");
            for (int i = 0; i < _current.Count; i++)
            {
                var f = _current[i];
                sb.Append("  [").Append(f.Verdict).Append('/').Append(f.Severity).Append("] ")
                  .Append(f.Component).Append(" - ").Append(f.EvidenceLine());
                if (!string.IsNullOrEmpty(f.Suggestion)) sb.Append(" -> ").Append(f.Suggestion);
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}

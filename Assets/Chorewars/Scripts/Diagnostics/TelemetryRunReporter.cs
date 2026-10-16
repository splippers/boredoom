using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Chorewars.Diagnostics
{
    /// <summary>
    /// Says, once per run, what the headset is and whether each component is working — as one
    /// <c>verdicts:</c> line a person or <c>tools/summarise-telemetry.py</c> can read.
    /// </summary>
    /// <remarks>
    /// The pieces of this already existed — <see cref="TelemetryWriter"/> writes, <see cref="TelemetrySamples"/>
    /// measures, <see cref="ExpectationRegistry"/> judges — but nothing ever *called* the registry, so
    /// no run ever produced a verdict and the one-page report said "No verdicts recorded." every time.
    /// This is the caller: it declares what the platform components should do, reports what they
    /// actually do, and emits the line.
    ///
    /// It is deliberately the only place that decides the platform checks, so the ranges live next to
    /// the values and there is one answer to "what did this build expect". A check is declared only on
    /// device: an editor that is not running XR has no display subsystem and no meshes, and reporting
    /// those as broken would be reporting the editor for being an editor.
    ///
    /// The line is emitted when the verdicts *change*, with a slow heartbeat so a steady run still
    /// proves it is alive, and at the moments the log could be lost — pause, quit, and an exception.
    /// Telemetry that is only in a buffer when the app is killed is telemetry that never happened.
    /// </remarks>
    public sealed class TelemetryRunReporter : MonoBehaviour
    {
        /// <summary>How often the platform is re-sampled. Not per frame: the values move slowly.</summary>
        const float SampleIntervalSeconds = 1f;

        /// <summary>A healthy run still says so this often, so silence never means "working".</summary>
        const float HeartbeatSeconds = 60f;

        // Check names. Read as the component, not the measurement.
        const string ReporterCheck = "reporter";
        const string XrDisplayCheck = "xrDisplay";
        const string ArCameraCheck = "arCamera";
        const string MeshSubsystemCheck = "meshSubsystems";

        readonly List<XRDisplaySubsystem> _displays = new List<XRDisplaySubsystem>(2);
        readonly List<XRMeshSubsystem> _meshes = new List<XRMeshSubsystem>(2);

        float _nextSampleAt;
        string _lastSummary;
        float _lastEmitAt;
        int _ticks;
        bool _hooked;

        static bool _warnedAboutDrops;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<TelemetryRunReporter>() != null) return;

            var go = new GameObject(nameof(TelemetryRunReporter));
            DontDestroyOnLoad(go);
            go.AddComponent<TelemetryRunReporter>();
        }

        void Start()
        {
            Hook();

            EmitRunHeader();

            // Ask before anything reads the room: meshing produces nothing until the scene permission
            // is granted, and the grant arrives a frame or more later, which the sampling below waits out.
            AndroidPermissions.RequestAll();
        }

        void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            Application.logMessageReceived += OnLogMessage;
        }

        void OnDestroy()
        {
            if (!_hooked) return;
            _hooked = false;

            Application.logMessageReceived -= OnLogMessage;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextSampleAt) return;

            _nextSampleAt = Time.unscaledTime + SampleIntervalSeconds;
            _ticks++;

            DeclarePlatformChecks(
                SampleXrDisplay(),
                SampleArCamera(),
                SampleMeshSubsystems(),
                _ticks,
                deviceOnly: !Application.isEditor);

            MaybeEmit();
        }

        // -- Sampling -------------------------------------------------------------------------------

        int SampleXrDisplay()
        {
            _displays.Clear();
            SubsystemManager.GetSubsystems(_displays);

            int running = 0;
            for (int i = 0; i < _displays.Count; i++)
                if (_displays[i] != null && _displays[i].running) running++;

            return running;
        }

        /// <summary>
        /// How many cameras AR Foundation is driving. A runtime with a camera subsystem but no manager
        /// has not been asked for passthrough at all, which is a different fault from "passthrough is on
        /// but the feed is broken", and this is the cheap half to check without a frame.
        /// </summary>
        int SampleArCamera()
        {
            var managers = FindObjectsByType<UnityEngine.XR.ARFoundation.ARCameraManager>(
                FindObjectsInactive.Exclude);

            return managers.Length;
        }

        int SampleMeshSubsystems()
        {
            _meshes.Clear();
            SubsystemManager.GetSubsystems(_meshes);

            int usable = 0;
            for (int i = 0; i < _meshes.Count; i++)
                if (_meshes[i] != null) usable++;

            return usable;
        }

        // -- Verdicts -------------------------------------------------------------------------------

        /// <summary>
        /// Declares and reports the platform checks. Static and fully parameterised so a test can drive
        /// it with injected values — the ranges are the thing worth checking, and they can be checked
        /// without a headset or a frame.
        /// </summary>
        public static void DeclarePlatformChecks(int xrDisplayRunning, int arCameras, int meshSubsystems,
            int ticks, bool deviceOnly)
        {
            // The one check that applies everywhere: if this never ticks, nothing else here was even
            // asked, so it is the difference between "the device is broken" and "the reporter is."
            ExpectationRegistry.Expect(
                ReporterCheck, ticks,
                ExpectationRange.AtLeast(1),
                ExpectationRange.AtLeast(0),
                "ticks", "the run reporter has sampled at least once");

            if (!deviceOnly) return;

            ExpectationRegistry.Expect(
                XrDisplayCheck, xrDisplayRunning,
                ExpectationRange.AtLeast(1), ExpectationRange.AtLeast(1),
                "running", "an XR display subsystem is running (the headset is rendering)");

            ExpectationRegistry.Expect(
                ArCameraCheck, arCameras,
                ExpectationRange.AtLeast(1), ExpectationRange.AtLeast(1),
                "managers", "AR Foundation is driving a camera (passthrough is wired)");

            // Zero here is the known failure this whole slice exists to find: the manifest declares the
            // scene permission and the package provides meshing, but until that permission is granted
            // and the feature is enabled, no subsystem appears and the scan silently falls back.
            ExpectationRegistry.Expect(
                MeshSubsystemCheck, meshSubsystems,
                ExpectationRange.AtLeast(1), ExpectationRange.AtLeast(1),
                "subsystems", "a mesh subsystem exists (room geometry can be read)");
        }

        void MaybeEmit()
        {
            string summary = ExpectationRegistry.Summary();
            bool changed = summary != _lastSummary;
            bool heartbeat = Time.unscaledTime - _lastEmitAt >= HeartbeatSeconds;

            if (!changed && !heartbeat) return;
            if (string.IsNullOrEmpty(summary)) return;

            _lastSummary = summary;
            _lastEmitAt = Time.unscaledTime;
            EmitVerdicts(changed ? "changed" : "heartbeat");
        }

        /// <summary>
        /// Writes the human <c>verdicts:</c> line, and flushes at once when anything is wrong so the
        /// verdict is on disk before a crash can take the buffer with it.
        /// </summary>
        public static void EmitVerdicts(string reason)
        {
            string summary = ExpectationRegistry.Summary();
            if (string.IsNullOrEmpty(summary)) return;

            TelemetryLogger.Info("Verdicts", "verdicts: " + summary);

            if (AnyBad())
            {
                TelemetryLogger.Warn("Verdicts", $"verdicts not all OK ({reason}) — flushing so they survive a kill.");
                FlushAndAccount();
            }
        }

        static bool AnyBad()
            => ExpectationRegistry.CountOf(ExpectationVerdict.Broken) > 0
               || ExpectationRegistry.CountOf(ExpectationVerdict.WayOff) > 0;

        // -- The run header -------------------------------------------------------------------------

        void EmitRunHeader() => TelemetryLogger.Info("Run", RunHeaderLine());

        /// <summary>
        /// The run header as a string: what the build is and what it is running on. Public and pure so
        /// it can be read and checked without a headset.
        /// </summary>
        public static string RunHeaderLine()
        {
            var header = new System.Text.StringBuilder("runHeader ");
            header.Append("game=").Append(Application.productName);
            header.Append(" version=").Append(Application.version);
            header.Append(" build=").Append(BuildGuid());
            header.Append(" unity=").Append(Application.unityVersion);
            header.Append(" editor=").Append(Application.isEditor);
            header.Append(" device=").Append(Safe(() => SystemInfo.deviceModel));
            header.Append(" os=").Append(Safe(() => SystemInfo.operatingSystem));
            header.Append(" utc=").Append(DateTime.UtcNow.ToString("o"));
            header.Append(" local=").Append(DateTime.Now.ToString("o"));
            header.Append(" permissions=").Append(AndroidPermissions.Summary());

            return header.ToString();
        }

        static string BuildGuid()
        {
            try { return string.IsNullOrEmpty(Application.buildGUID) ? "none" : Application.buildGUID; }
            catch (Exception) { return "unavailable"; }
        }

        /// <summary>A property that can throw on a platform that does not implement it reads as "unknown".</summary>
        static string Safe(Func<string> read)
        {
            try { return read() ?? "unknown"; }
            catch (Exception) { return "unknown"; }
        }

        // -- Where the log could be lost ------------------------------------------------------------

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;

            // Sampling here is cheap and the values are the most useful they will ever be: this is the
            // last moment before the OS may freeze or kill the process.
            DeclarePlatformChecks(SampleXrDisplay(), SampleArCamera(), SampleMeshSubsystems(), _ticks,
                deviceOnly: !Application.isEditor);
            EmitVerdicts("pause");
            FlushAndAccount();
        }

        void OnApplicationQuit()
        {
            DeclarePlatformChecks(SampleXrDisplay(), SampleArCamera(), SampleMeshSubsystems(), _ticks,
                deviceOnly: !Application.isEditor);
            EmitVerdicts("quit");
            EmitMachineReadableReport();
            FlushAndAccount();
        }

        void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;

            // An exception is exactly when the buffered lines matter most and exactly when the process
            // is most likely to be taken away. Flush the buffer before anything else runs.
            FlushAndAccount();
        }

        void EmitMachineReadableReport()
        {
            var json = ExpectationRegistry.Report();
            if (!string.IsNullOrEmpty(json))
                TelemetryLogger.Info("Verdicts", json);
        }

        /// <summary>Flushes, and says — once per run — if the ring overflowed and lines were lost.</summary>
        static void FlushAndAccount()
        {
            TelemetryWriter.Flush();

            int dropped = TelemetryWriter.DroppedLines;
            if (dropped <= 0 || _warnedAboutDrops) return;

            // Once, not once per flush: the point is to make the loss visible, and a warning repeated on
            // every error would itself flood the log it is complaining about.
            _warnedAboutDrops = true;
            TelemetryLogger.Warn("Telemetry",
                $"{dropped} telemetry line(s) dropped so far — the run outran the writer; the oldest part of a burst is missing.");
        }
    }
}

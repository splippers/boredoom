using System;
using Chorewars.Diagnostics;
using UnityEngine;

namespace Chorewars.AR
{
    /// <summary>
    /// Owns Space Setup requests and the one-room hardware checkpoint. Availability is not completion:
    /// measured geometry must arrive before the house reconstruction can be trusted.
    /// </summary>
    public sealed class HouseScanController : MonoBehaviour
    {
        public enum Readiness
        {
            EditorOnly, PermissionNeeded, SessionUnavailable, WaitingForGeometry,
            SurfacesOnly, MeshAvailable, RequestingSpaceSetup, SpaceSetupOpen, Error
        }

        [Header("Diagnostics")]
        [SerializeField] private float refreshSeconds = 0.5f;
        [SerializeField] private float requestTimeoutSeconds = 15f;
        [SerializeField] private float logIntervalSeconds = 10f;

        private SceneSurfaceReader _reader;
        private SpatialMeshTracker _tracker;
        private UnityEngine.XR.ARFoundation.ARSession _session;
        private float _nextRefresh;
        private float _nextLog;
        private float _requestedAt;
        private bool _sceneRequested;
        private bool _captureWasSuspended;
        private bool _measurementRequested;
        private bool _paused;
        private string _lastLog;
        private string _lastError;

        public Readiness Status { get; private set; } = Readiness.WaitingForGeometry;
        public string LastError => _lastError;
        public bool CaptureBusy => _sceneRequested;
        public bool SessionRunning => !_paused && _session != null && _session.subsystem != null
            && _session.subsystem.running && UnityEngine.XR.ARFoundation.ARSession.state
                == UnityEngine.XR.ARFoundation.ARSessionState.SessionTracking;
        public int SurfaceCount => _reader != null ? _reader.SurfaceCount : 0;
        public int MeshCount => _tracker != null ? _tracker.MeshCount : 0;
        public long TriangleCount => _tracker != null ? _tracker.TriangleCount : 0;
        public bool HasMeasuredMesh => Status == Readiness.MeshAvailable;
        public string Diagnostics { get; private set; } = "Waiting for spatial services.";
        public event Action Changed;

        public void Configure(SceneSurfaceReader reader, SpatialMeshTracker tracker,
            UnityEngine.XR.ARFoundation.ARSession session)
        {
            _reader = reader;
            _tracker = tracker;
            _session = session;
            _nextRefresh = 0f;
        }

        /// <summary>The same action serves the headset button and legacy request-file adapter.</summary>
        public bool RequestSceneCapture()
        {
            if (_sceneRequested) return false;
            if (!AndroidPermissions.IsDevice)
            {
                Fail("Space Setup requires a Quest, not the Editor.");
                return false;
            }
            if (!AndroidPermissions.SceneGranted)
            {
                AndroidPermissions.Request(AndroidPermissions.Scene, true);
                _lastError = "Allow spatial data, then select OPEN SPACE SETUP again.";
                RefreshStatus();
                return false;
            }
            var subsystem = _session != null
                ? _session.subsystem as UnityEngine.XR.OpenXR.Features.Meta.MetaOpenXRSessionSubsystem : null;
            if (subsystem == null || !subsystem.running)
            {
                Fail("Meta AR session is not ready. Resume the app and select RETRY READ.");
                return false;
            }
            try
            {
                if (!subsystem.TryRequestSceneCapture())
                {
                    Fail("The headset refused Space Setup. Existing house data is unchanged.");
                    return false;
                }
                _lastError = null;
                _sceneRequested = true;
                _captureWasSuspended = false;
                _requestedAt = Time.unscaledTime;
                Status = Readiness.RequestingSpaceSetup;
                TelemetryLogger.Info("HouseScan", "Space Setup accepted; waiting for headset suspension.");
                RefreshStatus();
                return true;
            }
            catch (Exception e)
            {
                Fail("Space Setup failed: " + e.Message);
                return false;
            }
        }

        /// <summary>Reload the device scene; neither clears saved rooms nor begins a chore.</summary>
        public void RetryRead()
        {
            if (_sceneRequested) return;
            _lastError = null;
            if (AndroidPermissions.IsDevice) AndroidPermissions.Request(AndroidPermissions.Scene, true);
            _reader?.RefreshScene();
            _tracker?.RefreshSubsystems();
            _measurementRequested = true;
            _nextRefresh = 0f;
            TelemetryLogger.Info("HouseScan", "Explicit measurement read requested; no house reset and no chore started.");
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (!_sceneRequested) return;
            if (paused)
            {
                _captureWasSuspended = true;
                Status = Readiness.SpaceSetupOpen;
                TelemetryLogger.Info("HouseScan", "Space Setup suspended the app.");
                return;
            }
            if (!_captureWasSuspended) return;
            _sceneRequested = false;
            _captureWasSuspended = false;
            RetryRead();
            TelemetryLogger.Info("HouseScan", "Returned from Space Setup; reading results. Return alone is not scan success.");
        }

        private void Update()
        {
            if (_sceneRequested && !_captureWasSuspended
                && Time.unscaledTime - _requestedAt > requestTimeoutSeconds)
            {
                _sceneRequested = false;
                Fail("Space Setup was accepted but did not suspend the app. Retry explicitly.");
            }
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + Mathf.Max(0.1f, refreshSeconds);
            _tracker?.RefreshSubsystems();
            if (_measurementRequested && SessionRunning && AndroidPermissions.SceneGranted && _tracker != null)
            {
                _tracker.StartScanning();
                _measurementRequested = false;
            }
            RefreshStatus();
        }

        private void Fail(string reason)
        {
            _lastError = reason;
            TelemetryLogger.Warn("HouseScan", reason);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            Status = _sceneRequested
                ? (_captureWasSuspended ? Readiness.SpaceSetupOpen : Readiness.RequestingSpaceSetup)
                : Evaluate(AndroidPermissions.IsDevice, _reader != null && _reader.PermissionGranted,
                    SessionRunning, SurfaceCount, MeshCount, TriangleCount);
            if (!_sceneRequested && !string.IsNullOrEmpty(_lastError)
                && Status != Readiness.PermissionNeeded && Status != Readiness.EditorOnly) Status = Readiness.Error;

            string message = MessageFor(Status);
            var counts = _reader != null ? _reader.Counts : null;
            Diagnostics = $"HOUSE SCAN — {message}\n" +
                $"Permission: {(_reader != null && _reader.PermissionGranted ? "granted" : "needed")}   Session: {(SessionRunning ? "tracking" : "not tracking")}\n" +
                $"Planes: {SurfaceCount}   Meshes: {MeshCount}   Triangles: {TriangleCount}\n" +
                $"Floors: {(counts != null ? counts[0] : 0)}   Walls: {(counts != null ? counts[2] : 0)}   Doors: {(counts != null ? counts[3] : 0)}\n" +
                $"Plane provider: {(_reader != null ? _reader.ProviderId : "none")} ({(_reader != null && _reader.ProviderRunning ? "running" : "stopped")})\n" +
                $"Mesh providers: {(_tracker != null ? _tracker.ProviderCount : 0)} ({(_tracker != null && _tracker.ProviderRunning ? "running" : "stopped")})\n" +
                "Alignment: not yet implemented — not a saved house scan.";
            if (!string.IsNullOrEmpty(_lastError)) Diagnostics += "\n" + _lastError;
            Changed?.Invoke();
            if (Diagnostics != _lastLog || Time.unscaledTime >= _nextLog)
            {
                _lastLog = Diagnostics;
                _nextLog = Time.unscaledTime + Mathf.Max(1f, logIntervalSeconds);
                TelemetryLogger.Info("HouseScan", Diagnostics.Replace('\n', ' '));
            }
        }

        public static Readiness Evaluate(bool device, bool permission, bool sessionRunning,
            int surfaces, int meshes, long triangles)
        {
            if (!device) return Readiness.EditorOnly;
            if (!permission) return Readiness.PermissionNeeded;
            if (!sessionRunning) return Readiness.SessionUnavailable;
            if (meshes > 0 && triangles > 0) return Readiness.MeshAvailable;
            if (surfaces > 0) return Readiness.SurfacesOnly;
            return Readiness.WaitingForGeometry;
        }

        private static string MessageFor(Readiness status)
        {
            switch (status)
            {
                case Readiness.EditorOnly: return "Quest required for capture";
                case Readiness.PermissionNeeded: return "spatial permission required";
                case Readiness.SessionUnavailable: return "resume headset / waiting for AR session";
                case Readiness.SurfacesOnly: return "simplified surfaces only; no measured mesh yet";
                case Readiness.MeshAvailable: return "measured mesh received; review required";
                case Readiness.RequestingSpaceSetup: return "opening Meta Space Setup";
                case Readiness.SpaceSetupOpen: return "Meta Space Setup is open";
                case Readiness.Error: return "request failed; existing house retained";
                default: return "no room geometry — open Space Setup or retry read";
            }
        }
    }
}

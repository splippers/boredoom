using System.Collections;
using System.Collections.Generic;
using Chorewars.Diagnostics;
using Chorewars.Dollhouse;
using Chorewars.UI;
using UnityEngine;

namespace Chorewars.Runtime
{
    public sealed class MetaDollhouseSetup : MonoBehaviour
    {
        [SerializeField] private MetaRoomMiniature _miniature;
        private Camera _camera;
        private PalmMiniatureController _palmPresentation;
        private UnityEngine.XR.ARFoundation.ARPlaneManager _planes;
        private bool _ownsPlanes;
        private bool _reload;
        private bool _enableNext;
        private bool _hasCurrentData;
        private float _nextRead;
        private int _lastHash = int.MinValue;
        private RectTransform _canvas;
        private UnityEngine.UI.Text _status;
        private UnityEngine.UI.Text _levelText;
        private UnityEngine.UI.Button _primary;
        private UnityEngine.UI.Button _hoover, _houseMode;
        private UnityEngine.UI.Text _houseModeText;
        private bool _floating;
        private UnityEngine.UI.Button _scanWhat, _scanGo;
        private UnityEngine.UI.Text _scanWhatText, _scanGoText, _scanStatus;
        private static readonly string[] ScanLabels = { "hoover", "toys", "clothes", "shoes", "room" };
        private int _scanLabel;
        private HooverSlice _slice;
        private string _lastStatus;
        private readonly List<MetaRoomMiniature.Surface> _surfaces = new List<MetaRoomMiniature.Surface>();
        private readonly Chorewars.HandsFree.GazePanelPlacement _placement =
            new Chorewars.HandsFree.GazePanelPlacement { Anchored = true };

        // Test switches (create an empty file with this name in the app's files folder): used to find what drops passthrough.
        private static bool Flag(string name) => System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, name));

        private void Awake()
        {
            _palmPresentation = GetComponent<PalmMiniatureController>();
            if (Flag("bisect-nopalm") && _palmPresentation != null)
            {
                _palmPresentation.enabled = false;
                TelemetryLogger.Info("CleanSetup", "TEST: palm miniature disabled.");
            }
        }

        private IEnumerator Start()
        {
            while ((_camera = Camera.main) == null) yield return null;
            _canvas = CleanMenu.CreateCanvas(transform, _camera, new Vector2(680f, 440f));
            CleanMenu.Background(_canvas);
            _status = CleanMenu.Text(_canvas, "MetaRoomStatus", "Waiting for Meta room data", new Vector2(0f, 140f), new Vector2(630f, 110f), 23);
            _primary = CleanMenu.Button(_canvas, "LevelOrRetry", "RETRY READ", new Vector2(-220f, 55f), new Vector2(204f, 64f), LevelOrRetry);
            _levelText = _primary.GetComponentInChildren<UnityEngine.UI.Text>();
            CleanMenu.Button(_canvas, "Recenter", "RECENTER", new Vector2(0f, 55f), new Vector2(204f, 64f), Recenter);
            CleanMenu.Button(_canvas, "BackToIntro", "BACK", new Vector2(220f, 55f), new Vector2(204f, 64f), Back);
            _houseMode = CleanMenu.Button(_canvas, "HouseMode", "HOUSE IN FRONT", new Vector2(-190f, -20f), new Vector2(280f, 64f), ToggleHouseMode);
            _houseModeText = _houseMode.GetComponentInChildren<UnityEngine.UI.Text>();
            _hoover = CleanMenu.Button(_canvas, "Hoover", "HOOVER THIS ROOM", new Vector2(150f, -20f), new Vector2(340f, 64f), StartHoover);
            _scanWhat = CleanMenu.Button(_canvas, "ScanWhat", "SCAN: HOOVER", new Vector2(-190f, -95f), new Vector2(280f, 64f), CycleScanLabel);
            _scanWhatText = _scanWhat.GetComponentInChildren<UnityEngine.UI.Text>();
            _scanGo = CleanMenu.Button(_canvas, "ScanGo", "START SCAN", new Vector2(150f, -95f), new Vector2(340f, 64f), ToggleScan);
            _scanGoText = _scanGo.GetComponentInChildren<UnityEngine.UI.Text>();
            _scanStatus = CleanMenu.Text(_canvas, "ScanStatus", "Scan: saves camera pictures on this headset only.", new Vector2(0f, -165f), new Vector2(630f, 44f), 20);
            if (!AndroidPermissions.IsDevice)
            {
                SetStatus("Quest required to read Meta Space Setup.\nNo simulated home is presented as measured data.");
                yield break;
            }
            if (Flag("bisect-noplanes")) { SetStatus("TEST: room planes and permission request skipped."); TelemetryLogger.Info("CleanSetup", "TEST: planes skipped."); yield break; }
            var origin = _camera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            if (origin == null) { SetStatus("XR origin is unavailable.\nReturn to intro and try again."); yield break; }
            _planes = origin.GetComponent<UnityEngine.XR.ARFoundation.ARPlaneManager>();
            _ownsPlanes = _planes == null;
            if (_planes == null) _planes = origin.gameObject.AddComponent<UnityEngine.XR.ARFoundation.ARPlaneManager>();
            _planes.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal | UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Vertical;
            _planes.enabled = AndroidPermissions.SceneGranted;
            AndroidPermissions.Request(AndroidPermissions.Scene);
            TelemetryLogger.Info("CleanSetup", "Reading Meta's saved Space Setup only; no custom scan, voice or profile restoration.");
        }

        private void Update()
        {
            if (_planes == null || _slice != null) return;
            if (_enableNext) { _enableNext = false; _planes.enabled = AndroidPermissions.SceneGranted; _nextRead = 0f; return; }
            if (_reload) { _reload = false; _planes.enabled = false; _enableNext = true; return; }
            bool granted = AndroidPermissions.SceneGranted;
            if (!granted)
            {
                _miniature?.SetVisible(false);
                SetStatus("Spatial permission is required.\nAllow it in headset settings, then RETRY READ.");
                return;
            }
            if (!_planes.enabled) { _planes.enabled = true; _nextRead = 0f; }
            bool tracking = UnityEngine.XR.ARFoundation.ARSession.state == UnityEngine.XR.ARFoundation.ARSessionState.SessionTracking;
            _miniature?.SetVisible(tracking && _hasCurrentData);
            if (!tracking) { SetStatus("Waiting for headset tracking.\nThe miniature stays hidden until tracking returns."); return; }
            if (Time.unscaledTime < _nextRead) return;
            _nextRead = Time.unscaledTime + 1f;
            ReadRoomModel();
        }

        private void ReadRoomModel()
        {
            _surfaces.Clear();
            int hash = 17;
            foreach (var plane in _planes.trackables)
            {
                if (plane.trackingState != UnityEngine.XR.ARSubsystems.TrackingState.Tracking) continue;
                var classification = plane.classifications;
                bool floor = (classification & UnityEngine.XR.ARSubsystems.PlaneClassifications.Floor) != 0;
                bool wall = (classification & (UnityEngine.XR.ARSubsystems.PlaneClassifications.WallFace |
                    UnityEngine.XR.ARSubsystems.PlaneClassifications.InnerWallFace |
                    UnityEngine.XR.ARSubsystems.PlaneClassifications.InvisibleWallFace)) != 0;
                if (!floor && !wall) continue;
                var boundary = plane.boundary;
                if (boundary.Length < 3) continue;
                string id = plane.trackableId.ToString();
                var corners = new Vector3[boundary.Length];
                unchecked
                {
                    hash = hash * 31 + id.GetHashCode();
                    hash = hash * 31 + (floor ? 1 : 0);
                    for (int i = 0; i < corners.Length; i++)
                    {
                        corners[i] = plane.transform.TransformPoint(new Vector3(boundary[i].x, 0f, boundary[i].y));
                        hash = hash * 31 + Mathf.RoundToInt(corners[i].x * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(corners[i].y * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(corners[i].z * 100f);
                    }
                }
                _surfaces.Add(new MetaRoomMiniature.Surface(id, floor, corners));
            }
            bool floorFound = false;
            foreach (var surface in _surfaces) if (surface.IsFloor) { floorFound = true; break; }
            if (!floorFound)
            {
                _hasCurrentData = false;
                _miniature?.SetVisible(false);
                SetStatus("No saved Meta floor surfaces are available.\nComplete Space Setup in headset Settings, then retry.");
                if (_levelText != null) _levelText.text = "RETRY READ";
                return;
            }
            if (_miniature == null) { SetStatus("Miniature configuration is missing."); return; }
            if (hash != _lastHash)
            {
                if (!_miniature.Rebuild(_surfaces, _camera.transform)) { SetStatus("Room surfaces arrived but could not be displayed.\nCheck the configured miniature materials."); return; }
                _lastHash = hash;
            }
            _hasCurrentData = true;
            _miniature.SetVisible(true);
            RefreshSummary();
        }
        private void RefreshSummary()
        {
            if (_miniature == null) return;
            SetStatus($"Your home: {_miniature.FloorCount} floor surfaces / {_miniature.Levels.Count} levels\n{_miniature.LevelLabel} — raise your left palm\nRight pinch: drag to turn, pull to zoom. Both pinches: stretch/turn.");
            if (_levelText != null) _levelText.text = _miniature.LevelLabel;
        }
        private void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
            if (_lastStatus == text) return;
            _lastStatus = text;
            TelemetryLogger.Info("CleanSetup", text.Replace('\n', ' '));
        }
        private void CycleScanLabel()
        {
            var cap = Chorewars.Diagnostics.FrameCapture.Instance;
            if (cap != null && cap.IsRunning) return; // label is fixed while a scan runs
            _scanLabel = (_scanLabel + 1) % ScanLabels.Length;
        }
        private void ToggleScan()
        {
            var cap = Chorewars.Diagnostics.FrameCapture.Instance;
            if (cap == null) { if (_scanStatus != null) _scanStatus.text = "Scanning is not available here."; return; }
            if (cap.IsRunning) cap.StopScan();
            else if (!cap.StartScan(ScanLabels[_scanLabel])) _scanStatus.text = "Could not start the scan.";
        }
        private void RefreshScanUi()
        {
            if (_scanGoText == null) return;
            var cap = Chorewars.Diagnostics.FrameCapture.Instance;
            bool running = cap != null && cap.IsRunning;
            _scanGoText.text = running ? "STOP SCAN" : "START SCAN";
            _scanWhatText.text = "SCAN: " + (running ? cap.Label : ScanLabels[_scanLabel]).ToUpperInvariant();
            if (running) _scanStatus.text = "SCANNING " + cap.Label + ": " + cap.Count + " pictures. Look around it, close and far.";
            else if (cap != null && cap.Count > 0) _scanStatus.text = "Last scan: " + cap.Count + " pictures saved on this headset.";
        }
        private void ToggleHouseMode()
        {
            if (_miniature == null) return;
            _floating = !_floating;
            if (_floating) _miniature.UseFloatingPresentation(); else _miniature.UsePalmPresentation();
            if (_palmPresentation != null) _palmPresentation.enabled = !_floating;
            if (_houseModeText != null) _houseModeText.text = _floating ? "HOUSE ON PALM" : "HOUSE IN FRONT";
            TelemetryLogger.Info("CleanSetup", _floating ? "House shown floating in front." : "House shown on the left palm.");
        }
        private void StartHoover()
        {
            if (_slice != null || !_hasCurrentData || _camera == null) { SetStatus("Hoover needs your room first.\nWait for 'Your home: ...' then try again."); return; }
            var head = _camera.transform.position; Vector3[] best = null; float bestScore = float.MaxValue;
            foreach (var s in _surfaces)
            {
                if (!s.IsFloor) continue;
                float y = 0f; foreach (var c in s.WorldCorners) y += c.y; y /= s.WorldCorners.Length;
                float score = Mathf.Abs(head.y - 1.2f - y) * 4f;
                if (!HooverGrid.Contains(s.WorldCorners, head.x, head.z))
                {
                    float cx = 0f, cz = 0f; foreach (var c in s.WorldCorners) { cx += c.x; cz += c.z; }
                    score += 10f + Vector2.Distance(new Vector2(head.x, head.z), new Vector2(cx / s.WorldCorners.Length, cz / s.WorldCorners.Length));
                }
                if (score < bestScore) { bestScore = score; best = s.WorldCorners; }
            }
            if (best == null) { SetStatus("No floor to hoover."); return; }
            _miniature?.SetVisible(false);
            _canvas.gameObject.SetActive(false);
            _slice = HooverSlice.Begin(transform, _camera, best, EndHoover);
        }
        private void EndHoover()
        {
            _slice = null;
            _canvas.gameObject.SetActive(true);
            _lastHash = int.MinValue; _nextRead = 0f;
        }
        public void RetryRead()
        {
            if (AndroidPermissions.IsDevice) AndroidPermissions.Request(AndroidPermissions.Scene, true);
            _lastHash = int.MinValue; _reload = true; _nextRead = 0f;
        }
        private void LevelOrRetry()
        {
            if (!_hasCurrentData || _miniature == null || _miniature.FloorCount == 0) RetryRead();
            else { _miniature.CycleLevel(); RefreshSummary(); }
        }
        public void Recenter()
        {
            _placement.Release();
            if (_palmPresentation != null) _palmPresentation.ResetView();
            else _miniature?.Recenter();
        }
        public void Back()=> UnityEngine.SceneManagement.SceneManager.LoadScene(Chorewars.Core.CleanExperience.IntroScene);
        private void OnApplicationPause(bool paused)
        {
            _hasCurrentData = false;
            _miniature?.SetVisible(false);
            if (!paused) { _lastHash = int.MinValue; _reload = true; }
        }
        private void LateUpdate()
        {
            if (_canvas != null && _camera != null) _placement.Hold(_canvas, _camera.transform, 1.35f, 0.5f);
            RefreshScanUi();
        }
        private void OnDestroy(){ if (_ownsPlanes && _planes != null) Destroy(_planes); }
    }
}

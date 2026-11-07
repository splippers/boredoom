using System.Collections.Generic;
using Chorewars.Diagnostics;
using Chorewars.Runtime;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Chorewars.AR
{
    /// <summary>
    /// Reads the room the player outlined in the headset's Space Setup (floor, ceiling, walls, door frames)
    /// through AR Foundation's plane manager, and puts the real floor at height zero.
    /// </summary>
    /// <remarks>
    /// With the boundary off the headset no longer promises where its floor is, so the game's floor drifted from
    /// the real one. A floor plane from Space Setup is a measurement, so the whole tracking origin is moved by that
    /// much once, and "floor = y 0" is true again for everything built on it.
    /// </remarks>
    public sealed class SceneSurfaceReader : MonoBehaviour
    {
        private const float PollSeconds = 1f;
        private const float FloorToleranceMeters = 0.03f;

        private ARPlaneManager _planes;
        private XROrigin _origin;
        private float _nextPoll;
        private float _nextLog;
        private int _stableFloorPolls;
        private float _lastFloorY = float.NaN;
        private bool _floorSet;
        private string _lastSummary;
        private float _nextRestart = 15f;
        private int _restarts;
        private bool _hadPermission;

        // A plane manager started before consent may need to reload once consent arrives.
        // Empty scenes receive bounded retries; the headset UI offers explicit Space Setup and recovery.
        private const float RestartEverySeconds = 15f;
        private const int MaxRestarts = 6;
        private const string ScenePermission = AndroidPermissions.Scene;

        public int SurfaceCount { get; private set; }

        /// <summary>Surfaces by kind (floor, ceiling, wall, door, window, other) at the last poll.</summary>
        public int[] Counts { get; } = new int[6];

        /// <summary>True when the headset has said yes to spatial data (always true off the headset).</summary>
        public bool PermissionGranted => !AndroidPermissions.IsDevice || UnityEngine.Android.Permission.HasUserAuthorizedPermission(ScenePermission);

        public int Restarts => _restarts;
        public bool FloorSet => _floorSet;
        public bool Initialized => _planes != null;
        public bool ProviderRunning => _planes != null && _planes.subsystem != null && _planes.subsystem.running;
        public string ProviderId => _planes != null && _planes.subsystem != null
            ? _planes.subsystem.subsystemDescriptor.id : "none";

        private bool _reloadPending;
        private bool _resumePlanes;
        private readonly List<SceneSurface> _pollSurfaces = new List<SceneSurface>();

        /// <summary>Reload the saved scene after Space Setup or an explicit retry, without erasing the house.</summary>
        public void RefreshScene()
        {
            _reloadPending = true;
            _nextPoll = 0f;
        }

        private void Start()
        {
            _origin = FindFirstObjectByType<XROrigin>();
            if (_origin == null)
            {
                TelemetryLogger.Warn("Scene", "no XR origin; cannot read the room outline.");
                return;
            }

            _planes = _origin.GetComponent<ARPlaneManager>();
            if (_planes == null) _planes = _origin.gameObject.AddComponent<ARPlaneManager>();
            _planes.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            _planes.enabled = true;
        }

        private void Update()
        {
            if (_planes == null) return;
            if (_resumePlanes)
            {
                _resumePlanes = false;
                _planes.enabled = true;
                _nextPoll = 0f;
                return;
            }
            if (_reloadPending)
            {
                _reloadPending = false;
                _planes.enabled = false;
                _resumePlanes = true;
                SurfaceCount = 0;
                System.Array.Clear(Counts, 0, Counts.Length);
                return;
            }

            KeepAskingForSpatialData();
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollSeconds;

            _pollSurfaces.Clear();
            ReadSurfaces(_pollSurfaces);
            SurfaceCount = _pollSurfaces.Count;
            Summarise(_pollSurfaces);
            AlignFloor(_pollSurfaces);
        }

        private void KeepAskingForSpatialData()
        {
            if (!AndroidPermissions.IsDevice) return;

            bool granted = AndroidPermissions.SceneGranted;
            if (!granted)
            {
                _hadPermission = false;
                AndroidPermissions.Request(ScenePermission);
                return;
            }

            // The answer arrived after the plane manager started: restart it so it picks the room up.
            if (!_hadPermission)
            {
                _hadPermission = true;
                if (_planes.trackables.count == 0) Restart("spatial data permission arrived after the plane manager started");
                return;
            }

            // Still nothing a while after the permission is held: restart a few times before giving up.
            if (SurfaceCount == 0 && _restarts < MaxRestarts && Time.unscaledTime >= _nextRestart)
                Restart("no room surfaces yet");
        }

        private void Restart(string why)
        {
            _restarts++;
            _nextRestart = Time.unscaledTime + RestartEverySeconds;
            RefreshScene();
            TelemetryLogger.Info("Scene", $"plane manager restart queued ({why}); restart {_restarts}");
        }

        public void ReadSurfaces(List<SceneSurface> into)
        {
            if (_planes == null) return;

            foreach (var plane in _planes.trackables)
            {
                var kind = KindOf(plane.classifications);
                var surface = new SceneSurface { kind = kind, center = plane.center };

                if (kind == SceneSurfaceKind.Floor || kind == SceneSurfaceKind.Ceiling)
                {
                    var boundary = plane.boundary;
                    var outline = new Vector2[boundary.Length];
                    for (int i = 0; i < boundary.Length; i++)
                    {
                        var w = plane.transform.TransformPoint(new Vector3(boundary[i].x, 0f, boundary[i].y));
                        outline[i] = new Vector2(w.x, w.z);
                    }
                    surface.outlineXZ = outline;
                }

                into.Add(surface);
            }
        }

        /// <summary>Corners, in world space, of every door-frame surface the headset has outlined.</summary>
        public void ReadDoorOutlines(List<Vector3[]> into)
        {
            if (_planes == null) return;

            foreach (var plane in _planes.trackables)
            {
                if (KindOf(plane.classifications) != SceneSurfaceKind.Door) continue;

                var boundary = plane.boundary;
                if (boundary.Length < 3) continue;

                var corners = new Vector3[boundary.Length];
                for (int i = 0; i < boundary.Length; i++)
                    corners[i] = plane.transform.TransformPoint(new Vector3(boundary[i].x, 0f, boundary[i].y));
                into.Add(corners);
            }
        }

        private static SceneSurfaceKind KindOf(PlaneClassifications c)
        {
            if ((c & PlaneClassifications.Floor) != 0) return SceneSurfaceKind.Floor;
            if ((c & PlaneClassifications.Ceiling) != 0) return SceneSurfaceKind.Ceiling;
            if ((c & PlaneClassifications.DoorFrame) != 0) return SceneSurfaceKind.Door;
            if ((c & PlaneClassifications.WindowFrame) != 0) return SceneSurfaceKind.Window;
            if ((c & (PlaneClassifications.WallFace | PlaneClassifications.InnerWallFace | PlaneClassifications.InvisibleWallFace)) != 0)
                return SceneSurfaceKind.Wall;
            return SceneSurfaceKind.Other;
        }

        private void Summarise(List<SceneSurface> surfaces)
        {
            var counts = new int[6];
            foreach (var s in surfaces) counts[(int)s.kind]++;
            System.Array.Copy(counts, Counts, 6);

            string summary = $"floors={counts[0]} ceilings={counts[1]} walls={counts[2]} doors={counts[3]} windows={counts[4]} other={counts[5]}";
            if (summary == _lastSummary && Time.unscaledTime < _nextLog) return;

            _lastSummary = summary;
            _nextLog = Time.unscaledTime + 30f;
            TelemetryLogger.Info("Scene", "room outline from the headset: " + summary);
        }

        private void AlignFloor(List<SceneSurface> surfaces)
        {
            if (_floorSet || _origin == null) return;

            float lowest = float.NaN;
            foreach (var s in surfaces)
                if (s.kind == SceneSurfaceKind.Floor && (float.IsNaN(lowest) || s.center.y < lowest)) lowest = s.center.y;

            if (float.IsNaN(lowest)) { _stableFloorPolls = 0; return; }

            // Two reads that agree, so a plane still being refined is not acted on.
            _stableFloorPolls = !float.IsNaN(_lastFloorY) && Mathf.Abs(lowest - _lastFloorY) < 0.01f ? _stableFloorPolls + 1 : 0;
            _lastFloorY = lowest;
            if (_stableFloorPolls < 2) return;

            _floorSet = true;
            if (Mathf.Abs(lowest) <= FloorToleranceMeters)
            {
                TelemetryLogger.Info("Scene", $"floor already at the real floor (y={lowest:0.00}).");
                return;
            }

            var p = _origin.transform.position;
            p.y -= lowest;
            _origin.transform.position = p;
            TelemetryLogger.Info("Scene", $"floor was {lowest:0.00} m off; the tracking origin moved so the real floor is y=0.");
        }
    }
}

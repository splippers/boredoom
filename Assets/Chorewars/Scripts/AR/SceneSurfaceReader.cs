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

        public int SurfaceCount { get; private set; }
        public bool FloorSet => _floorSet;

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
            _planes.enabled = true;
        }

        private void Update()
        {
            if (_planes == null || Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollSeconds;

            var surfaces = new List<SceneSurface>();
            ReadSurfaces(surfaces);
            SurfaceCount = surfaces.Count;
            Summarise(surfaces);
            AlignFloor(surfaces);
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

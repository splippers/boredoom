using System.Collections.Generic;
using System.IO;
using Chorewars.Diagnostics;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR.Features.Meta;

namespace Chorewars.AR
{
    /// <summary>
    /// Makes every doorway the headset has found glow, and starts the headset's own room scan on request.
    /// </summary>
    /// <remarks>
    /// The headset only knows a door once it has been through Space Setup (its "scene capture"), which labels
    /// walls, furniture and door frames. A <c>scene-capture.request</c> file asks the headset to open that scan from inside
    /// the game; the game pauses while the player scans, then wakes up and finds the doors by itself. Each door
    /// frame the headset reports is outlined in a pulsing glow, and the log says how many were found.
    /// </remarks>
    public sealed class DoorGlow : MonoBehaviour
    {
        const float PollSeconds = 1f;
        const float LineWidth = 0.05f;

        SceneSurfaceReader _reader;
        float _nextPoll;
        float _nextFilePoll;
        int _lastCount = -1;
        bool _capturing;
        Material _material;
        readonly List<Vector3[]> _doors = new List<Vector3[]>();
        readonly List<LineRenderer> _lines = new List<LineRenderer>();

        public int DoorCount => _doors.Count;

        public void Configure(SceneSurfaceReader reader)
        {
            _reader = reader;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) _material = new Material(shader);
        }

        /// <summary>Asks the headset to open Space Setup. Returns false when the headset refuses.</summary>
        public bool RequestSceneCapture()
        {
            var session = FindFirstObjectByType<ARSession>();
            var subsystem = session != null ? session.subsystem as MetaOpenXRSessionSubsystem : null;
            if (subsystem == null)
            {
                TelemetryLogger.Warn("Scene", "scene capture: no Meta session to ask (not on the headset?)");
                return false;
            }

            bool ok = subsystem.TryRequestSceneCapture();
            _capturing = ok;
            TelemetryLogger.Info("Scene", ok
                ? "scene capture requested: the headset will open its room scan now"
                : "scene capture was refused by the headset");
            return ok;
        }

        void OnApplicationPause(bool paused)
        {
            if (!_capturing) return;
            if (paused) { TelemetryLogger.Info("Scene", "scene capture: the headset's room scan is open"); return; }

            _capturing = false;
            _nextPoll = 0f;
            TelemetryLogger.Info("Scene", "scene capture finished; reading the room it saved");
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextFilePoll)
            {
                _nextFilePoll = Time.unscaledTime + 0.5f;
                var request = Path.Combine(Application.persistentDataPath, "scene-capture.request");
                if (File.Exists(request))
                {
                    try { File.Delete(request); } catch { }
                    RequestSceneCapture();
                }
            }

            if (_reader != null && Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + PollSeconds;
                _doors.Clear();
                _reader.ReadDoorOutlines(_doors);
                Rebuild();
            }

            Pulse();
        }

        void Rebuild()
        {
            if (_doors.Count != _lastCount)
            {
                _lastCount = _doors.Count;
                TelemetryLogger.Info("Scene", $"doorways glowing: {_doors.Count}");
            }

            while (_lines.Count < _doors.Count) _lines.Add(NewLine());

            for (int i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                bool used = i < _doors.Count;
                line.gameObject.SetActive(used);
                if (!used) continue;

                var corners = _doors[i];
                line.positionCount = corners.Length;
                line.SetPositions(corners);
            }
        }

        LineRenderer NewLine()
        {
            var go = new GameObject("DoorGlow");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = LineWidth;
            line.numCapVertices = 4;
            if (_material != null) line.sharedMaterial = _material;
            return line;
        }

        void Pulse()
        {
            float t = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);
            var colour = new Color(0.4f * t, 1f * t, 0.9f * t, 1f);
            foreach (var line in _lines)
            {
                if (!line.gameObject.activeSelf) continue;
                line.startColor = colour;
                line.endColor = colour;
                line.widthMultiplier = LineWidth * (0.8f + 0.5f * t);
            }
        }
    }
}

using System.Collections.Generic;
using Chorewars.Diagnostics;
using Chorewars.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Chorewars.Runtime
{
    /// <summary>
    /// One-room hoover session for the clean AR build. Dirt covers the scanned floor; sweep your right hand (or, with no hand,
    /// walk) over it to clean. Self-contained: no Claudia, voice link or legacy chore code.
    /// </summary>
    public sealed class HooverSlice : MonoBehaviour
    {
        const float CellSize = 0.25f, Radius = 0.35f, Duration = 120f, FloorLift = 0.01f;
        Camera _camera; Transform _trackingSpace;
        List<Vector2> _cells; bool[] _clean; int _cleanCount;
        Mesh _mesh; Color32[] _colors; float _floorY;
        Transform _cursor; RectTransform _canvas; Text _hud; Button _again;
        readonly Chorewars.HandsFree.GazePanelPlacement _placement = new Chorewars.HandsFree.GazePanelPlacement { Anchored = true };
        float _remaining; bool _running;
        System.Action _exit;
        Vector3[] _floor;
        System.Collections.Generic.List<UnityEngine.XR.Hands.XRHandSubsystem> _subs = new List<UnityEngine.XR.Hands.XRHandSubsystem>();
        static readonly Color32 Dirt = new Color32(120, 78, 40, 150), Clear = new Color32(0, 0, 0, 0);

        public static HooverSlice Begin(Transform parent, Camera camera, Vector3[] floorCorners, System.Action onExit)
        {
            var go = new GameObject("HooverSlice");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<HooverSlice>();
            s._camera = camera; s._floor = floorCorners; s._exit = onExit;
            s.Setup();
            return s;
        }

        void Setup()
        {
            var origin = _camera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            _trackingSpace = origin != null ? (origin.Origin != null ? origin.Origin.transform : origin.transform) : null;
            float y = 0f; foreach (var c in _floor) y += c.y; _floorY = y / _floor.Length + FloorLift;
            _cells = HooverGrid.Cells(_floor, CellSize);
            if (_cells.Count == 0) { TelemetryLogger.Warn("HooverSlice", "Floor polygon produced no cells."); Exit(); return; }
            BuildMesh();
            var cursor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(cursor.GetComponent<Collider>());
            cursor.transform.SetParent(transform, false);
            cursor.transform.localScale = new Vector3(Radius * 2f, 0.01f, Radius * 2f);
            cursor.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.2f, 0.8f, 1f, 0.45f) };
            _cursor = cursor.transform;
            _canvas = CleanMenu.CreateCanvas(transform, _camera, new Vector2(560f, 240f));
            CleanMenu.Background(_canvas);
            _hud = CleanMenu.Text(_canvas, "HooverHud", "", new Vector2(0f, 45f), new Vector2(520f, 120f), 30);
            _again = CleanMenu.Button(_canvas, "Again", "AGAIN", new Vector2(-140f, -75f), new Vector2(240f, 64f), Restart);
            CleanMenu.Button(_canvas, "Done", "BACK", new Vector2(140f, -75f), new Vector2(240f, 64f), Exit);
            Restart();
            TelemetryLogger.Info("HooverSlice", $"Started: {_cells.Count} cells on floor y={_floorY:0.00}.");
        }

        void BuildMesh()
        {
            int n = _cells.Count, h = 0;
            var v = new Vector3[n * 4]; var t = new int[n * 6]; _colors = new Color32[n * 4];
            float e = CellSize * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var c = _cells[i];
                v[i * 4] = new Vector3(c.x - e, _floorY, c.y - e); v[i * 4 + 1] = new Vector3(c.x - e, _floorY, c.y + e);
                v[i * 4 + 2] = new Vector3(c.x + e, _floorY, c.y + e); v[i * 4 + 3] = new Vector3(c.x + e, _floorY, c.y - e);
                t[h++] = i * 4; t[h++] = i * 4 + 1; t[h++] = i * 4 + 2; t[h++] = i * 4; t[h++] = i * 4 + 2; t[h++] = i * 4 + 3;
            }
            _mesh = new Mesh { name = "HooverDirt", indexFormat = n * 4 > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            _mesh.vertices = v; _mesh.triangles = t;
            var go = new GameObject("Dirt", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = _mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        void Restart()
        {
            _clean = new bool[_cells.Count]; _cleanCount = 0;
            for (int i = 0; i < _colors.Length; i++) _colors[i] = Dirt;
            _mesh.SetColors(_colors);
            _remaining = Duration; _running = true;
            if (_again != null) _again.gameObject.SetActive(false);
        }

        void Exit() { TelemetryLogger.Info("HooverSlice", "Exited."); _exit?.Invoke(); Destroy(gameObject); }

        bool ToolPoint(out Vector3 p)
        {
            p = default;
            if (_trackingSpace != null)
            {
                _subs.Clear(); SubsystemManager.GetSubsystems(_subs);
                foreach (var s in _subs)
                {
                    if (!s.running || !s.rightHand.isTracked) continue;
                    if (s.rightHand.GetJoint(UnityEngine.XR.Hands.XRHandJointID.IndexTip).TryGetPose(out var pose)) { p = _trackingSpace.TransformPoint(pose.position); return true; }
                }
            }
            // No hand: a point 0.7 m ahead of the head on the floor (walking cleans).
            var f = _camera.transform.forward; f.y = 0f; f = f.sqrMagnitude < 0.01f ? Vector3.forward : f.normalized;
            p = _camera.transform.position + f * 0.7f;
            return false;
        }

        void Update()
        {
            if (_camera == null || _cells == null) return;
            ToolPoint(out var tool);
            _cursor.position = new Vector3(tool.x, _floorY + 0.005f, tool.z);
            if (_running)
            {
                _remaining -= Time.unscaledDeltaTime;
                int n = HooverGrid.Clean(_cells, _clean, tool.x, tool.z, Radius);
                if (n > 0)
                {
                    _cleanCount += n;
                    for (int i = 0; i < _clean.Length; i++) if (_clean[i] && _colors[i * 4].a != 0) for (int k = 0; k < 4; k++) _colors[i * 4 + k] = Clear;
                    _mesh.SetColors(_colors);
                }
                int pct = Mathf.RoundToInt(100f * _cleanCount / _cells.Count);
                if (pct >= 100 || _remaining <= 0f)
                {
                    _running = false; _again.gameObject.SetActive(true);
                    _hud.text = $"{(pct >= 100 ? "Spotless!" : "Time!")}\nCleaned {pct}%";
                    TelemetryLogger.Info("HooverSlice", $"Finished: {pct}% with {Mathf.Max(0f, _remaining):0}s left.");
                }
                else _hud.text = $"Sweep your right hand over the floor\nCleaned {pct}%   Time {Mathf.CeilToInt(_remaining)}s";
            }
        }

        void LateUpdate() { if (_canvas != null && _camera != null) _placement.Hold(_canvas, _camera.transform, 1.3f, -0.25f); }
    }
}

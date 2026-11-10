using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.Dollhouse
{
    public sealed class MetaRoomMiniature : MonoBehaviour
    {
        public sealed class Surface
        {
            public readonly string Id;
            public readonly bool IsFloor;
            public readonly Vector3[] WorldCorners;
            public Surface(string id, bool floor, Vector3[] corners)
            {
                Id = id; IsFloor = floor; WorldCorners = corners != null ? (Vector3[])corners.Clone() : Array.Empty<Vector3>();
            }
        }
        private sealed class Part
        {
            public GameObject Object;
            public int Level;
            public Bounds Bounds;
        }
        [SerializeField] private Material _floorMaterial;
        [SerializeField] private Material _wallMaterial;
        [SerializeField] private float _widthMeters = 0.75f;
        [SerializeField] private float _levelTolerance = 0.5f;
        private readonly List<float> _levels = new List<float>();
        private readonly List<Part> _parts = new List<Part>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Transform _content;
        private Transform _head;
        private Vector3 _sourceOrigin;
        private Vector3 _displayCentre;
        private Vector3 _palmAnchorLocal;
        private float _fitScale = 1f;
        private bool _palmDriven;
        private bool _palmVisible;
        private bool _sourceVisible = true;
        public int FloorCount { get; private set; }
        public bool PalmDriven => _palmDriven;

        public void UsePalmPresentation()
        {
            _palmDriven = true;
            _palmVisible = false;
            ApplyVisibility();
        }
        /// <summary>Show the house floating in front of the player instead of on the palm (fallback when the palm is not tracked).</summary>
        public void UseFloatingPresentation()
        {
            _palmDriven = false;
            if (_content != null) _content.localScale = Vector3.one * _fitScale;
            Recenter();
            ApplyVisibility();
        }
        public void SetPalmVisible(bool visible) { _palmVisible = visible; ApplyVisibility(); }
        public void SetPalmPose(Vector3 bottomPoint, float yawDegrees, float zoom)
        {
            if (!_palmDriven || _content == null) return;
            float scale = _fitScale * Mathf.Clamp(zoom, 0.4f, 2.5f);
            var rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            _content.localScale = Vector3.one * scale;
            _content.SetPositionAndRotation(bottomPoint - rotation * (_palmAnchorLocal * scale), rotation);
        }
        private void ApplyVisibility()
        {
            if (_content != null) _content.gameObject.SetActive(_sourceVisible && (!_palmDriven || _palmVisible));
        }
        public IReadOnlyList<float> Levels => _levels;
        public int SelectedLevel { get; private set; } = -1;
        public string LevelLabel => SelectedLevel < 0 ? "ALL LEVELS" : "LEVEL " + (SelectedLevel + 1);

        public static List<float> GroupLevels(IEnumerable<float> elevations, float tolerance)
        {
            var sorted = new List<float>(elevations);
            sorted.Sort();
            var levels = new List<float>();
            foreach (float y in sorted)
                if (levels.Count == 0 || Mathf.Abs(y - levels[levels.Count - 1]) > tolerance) levels.Add(y);
            return levels;
        }

        public bool Rebuild(IReadOnlyList<Surface> surfaces, Transform head)
        {
            if (_floorMaterial == null || _wallMaterial == null || surfaces == null || surfaces.Count == 0) return false;
            foreach (var surface in surfaces)
            {
                if (surface == null || surface.WorldCorners.Length < 3) return false;
                foreach (var p in surface.WorldCorners)
                    if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z)) return false;
            }
            var elevations = new List<float>();
            foreach (var s in surfaces)
                if (s.IsFloor && s.WorldCorners.Length >= 3) elevations.Add(MeanY(s.WorldCorners));
            if (elevations.Count == 0) return false;
            ClearGeometry();
            FloorCount = elevations.Count;
            elevations.Sort();
            _levels.Clear();
            _levels.AddRange(GroupLevels(elevations, _levelTolerance));
            if (SelectedLevel >= _levels.Count) SelectedLevel = -1;
            _head = head;
            Bounds total = new Bounds(surfaces[0].WorldCorners[0], Vector3.zero);
            foreach (var s in surfaces) foreach (var p in s.WorldCorners) total.Encapsulate(p);
            _sourceOrigin = total.center;
            _content = new GameObject("Measured Room Miniature").transform;
            _content.SetParent(transform, false);
            _content.gameObject.SetActive(false);
            foreach (var surface in surfaces)
            {
                if (surface.WorldCorners.Length < 3) continue;
                var indices = Triangulate(surface.WorldCorners);
                if (indices.Length == 0) continue;
                var go = new GameObject("Meta Surface " + surface.Id, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(_content, false);
                var local = new Vector3[surface.WorldCorners.Length];
                Bounds bounds = new Bounds(surface.WorldCorners[0], Vector3.zero);
                for (int i = 0; i < local.Length; i++) { local[i] = surface.WorldCorners[i] - _sourceOrigin; bounds.Encapsulate(surface.WorldCorners[i]); }
                var mesh = new Mesh { name = "Meta polygon " + surface.Id };
                mesh.vertices = local; mesh.triangles = indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                _meshes.Add(mesh);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = surface.IsFloor ? _floorMaterial : _wallMaterial;
                float baseY = surface.IsFloor ? MeanY(surface.WorldCorners) : bounds.min.y;
                int level = 0;
                for (int i = 1; i < _levels.Count; i++)
                    if (Mathf.Abs(baseY - _levels[i]) < Mathf.Abs(baseY - _levels[level])) level = i;
                _parts.Add(new Part { Object = go, Level = level, Bounds = bounds });
            }
            ApplyLevel();
            return true;
        }
        public void CycleLevel()
        {
            if (_levels.Count == 0) return;
            SelectedLevel++;
            if (SelectedLevel >= _levels.Count) SelectedLevel = -1;
            ApplyLevel();
        }
        private void ApplyLevel()
        {
            bool found = false; Bounds visible = default;
            foreach (var part in _parts)
            {
                bool enabled = SelectedLevel < 0 || part.Level == SelectedLevel;
                part.Object.SetActive(enabled);
                if (!enabled) continue;
                if (!found) { visible = part.Bounds; found = true; } else visible.Encapsulate(part.Bounds);
            }
            if (!found || _content == null) return;
            float extent = Mathf.Max(visible.size.x, Mathf.Max(visible.size.y, visible.size.z));
            _fitScale = _widthMeters / Mathf.Max(0.1f, extent);
            _content.localScale = Vector3.one * _fitScale;
            _displayCentre = visible.center - _sourceOrigin;
            _palmAnchorLocal = new Vector3(visible.center.x, visible.min.y, visible.center.z) - _sourceOrigin;
            Recenter();
            ApplyVisibility();
        }
        public void Recenter()
        {
            if (_palmDriven || _head == null || _content == null) return;
            var forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.ProjectOnPlane(_head.up, Vector3.up).normalized;
            Vector3 target = _head.position + forward * 0.95f + Vector3.down * 0.1f;
            _content.position = target - _displayCentre * _content.localScale.x;
        }
        public bool IsVisible => _content != null && _content.gameObject.activeInHierarchy;
        public void SetVisible(bool visible) { _sourceVisible = visible; ApplyVisibility(); }
        private void OnDestroy() => ClearGeometry();
        private void ClearGeometry()
        {
            if (_content != null) { _content.gameObject.SetActive(false); Destroy(_content.gameObject); _content = null; }
            foreach (var mesh in _meshes) if (mesh != null) Destroy(mesh);
            _meshes.Clear(); _parts.Clear();
        }
        private static float MeanY(Vector3[] points)
        {
            float y = 0f; foreach (var p in points) y += p.y; return y / points.Length;
        }
        /// <summary>Ear clipping in the surface's own plane; supports concave floor outlines and vertical walls.</summary>
        public static int[] Triangulate(Vector3[] points)
        {
            if (points == null || points.Length < 3) return Array.Empty<int>();
            Vector3 tangent = (points[1] - points[0]).normalized;
            Vector3 normal = Vector3.zero;
            for (int i = 2; i < points.Length; i++)
            {
                normal = Vector3.Cross(tangent, points[i] - points[0]);
                if (normal.sqrMagnitude > 0.000001f) break;
            }
            if (normal.sqrMagnitude < 0.000001f) return Array.Empty<int>();
            Vector3 bitangent = Vector3.Cross(normal.normalized, tangent);
            var p2 = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
            { var p = points[i] - points[0]; p2[i] = new Vector2(Vector3.Dot(p, tangent), Vector3.Dot(p, bitangent)); }
            float area = 0f;
            for (int i = 0; i < p2.Length; i++) area += Cross(p2[i], p2[(i + 1) % p2.Length]);
            var remaining = new List<int>();
            for (int i = 0; i < p2.Length; i++) remaining.Add(area >= 0f ? i : p2.Length - 1 - i);
            var triangles = new List<int>();
            int guard = points.Length * points.Length;
            while (remaining.Count > 2 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(p2[b] - p2[a], p2[c] - p2[b]) <= 0.000001f) continue;
                    bool inside = false;
                    foreach (int q in remaining)
                    {
                        if (q == a || q == b || q == c) continue;
                        if (Cross(p2[b] - p2[a], p2[q] - p2[a]) >= -0.000001f &&
                            Cross(p2[c] - p2[b], p2[q] - p2[b]) >= -0.000001f &&
                            Cross(p2[a] - p2[c], p2[q] - p2[c]) >= -0.000001f) { inside = true; break; }
                    }
                    if (inside) continue;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) return Array.Empty<int>();
            }
            return triangles.ToArray();
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}

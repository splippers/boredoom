using System.Collections.Generic;
using System.IO;
using Chorewars.Diagnostics;
using Chorewars.Voice;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Maps how much of a room's floor can really be used: a grid over the room that turns green where the
    /// player has walked and red where furniture stands.
    /// </summary>
    /// <remarks>
    /// The room's outline comes from its ceiling scan. Start with a <c>floor-scan.request</c> file holding the room's
    /// name, then walk the free floor (it fills green by itself). To mark furniture, look at the floor where it
    /// meets the floor at each corner and hold still; look back at the first marker to close it, and those cells
    /// turn red. <c>floor-done.request</c> saves the grid to <c>floor-grids.json</c> beside the house map and logs the
    /// areas; <c>floor-cancel.request</c> throws it away.
    /// </remarks>
    public sealed class FloorScanner : MonoBehaviour
    {
        const float DwellSeconds = 1.2f;
        const float SteadyMeters = 0.2f;
        const float WalkRadius = 0.45f;
        const float MaxReach = 4.5f;
        const float CellSize = 0.25f;

        HouseMapDriver _driver;
        Transform _head;
        bool _active;
        string _name;
        float _floorY;
        FloorGrid _grid;
        float _nextPoll, _nextPaint, _nextTrace;
        bool _dirty;

        readonly List<Vector2> _corners = new List<Vector2>();
        readonly DwellButtonLogic _dwell = new DwellButtonLogic { Seconds = DwellSeconds, DrainSeconds = 0.3f };
        Vector3 _steadyAt;
        bool _haveSteady;

        Texture2D _texture;
        GameObject _quad;
        Transform _reticle;
        LineRenderer _line;
        TextMesh _help;
        Material _lineMaterial;
        readonly List<GameObject> _markers = new List<GameObject>();

        public bool Active => _active;

        public void Configure(HouseMapDriver driver, Transform head)
        {
            _driver = driver;
            _head = head;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) _lineMaterial = new Material(shader);
        }

        public bool Begin(string roomName)
        {
            if (_driver == null || string.IsNullOrEmpty(roomName)) return false;

            RoomScan found = default;
            bool have = false;
            foreach (var room in _driver.Rooms)
            {
                if (room.assumed) continue;
                if (string.Equals(_driver.NameOf(room.roomId), roomName, System.StringComparison.OrdinalIgnoreCase)) { found = room; have = true; break; }
            }

            if (!have)
            {
                TelemetryLogger.Info("Floor", $"floor scan: no room called \"{roomName}\" yet (scan its ceiling first)");
                return false;
            }

            Teardown();
            _name = roomName;
            _floorY = found.floorY;
            _grid = new FloorGrid(found.boundsXZ, CellSize);
            _corners.Clear();
            _haveSteady = false;
            _active = true;
            Build();
            TelemetryLogger.Info("Floor", $"floor scan started for \"{roomName}\": {found.boundsXZ.width:0.0} x {found.boundsXZ.height:0.0} m, {_grid.Columns} x {_grid.Rows} squares");
            return true;
        }

        void Update()
        {
            Poll();
            if (!_active || _head == null) return;

            var head = _head.position;
            if (_grid.MarkWalked(new Vector2(head.x, head.z), WalkRadius) > 0) _dirty = true;

            Aim();

            if (_dirty && Time.unscaledTime >= _nextPaint)
            {
                _nextPaint = Time.unscaledTime + 0.25f;
                _dirty = false;
                Paint();
            }
        }

        void Aim()
        {
            var dir = _head.forward;
            bool onFloor = dir.y < -0.2f;
            float t = onFloor ? (_floorY - _head.position.y) / dir.y : 0f;
            if (!onFloor || t > MaxReach)
            {
                _reticle.gameObject.SetActive(false);
                _dwell.Update(false, Time.unscaledDeltaTime);
                _haveSteady = false;
                SetHelp(_head.position + _head.forward * 1.5f, Prompt());
                return;
            }

            var hit = _head.position + dir * t;
            _reticle.gameObject.SetActive(true);
            _reticle.position = hit;

            if (!_haveSteady || Vector3.Distance(_steadyAt, hit) > SteadyMeters)
            {
                _steadyAt = hit;
                _haveSteady = true;
                _dwell.Update(false, 10f);
            }

            bool fired = _dwell.Update(true, Time.unscaledDeltaTime);
            float progress = _dwell.Progress01;
            _reticle.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.2f, progress);
            _reticle.GetComponent<MeshRenderer>().sharedMaterial.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.2f), progress);
            SetHelp(hit + Vector3.up * 0.3f, Prompt());
            if (!fired) return;

            var p = new Vector2(hit.x, hit.z);
            if (_corners.Count >= 3 && (p - _corners[0]).magnitude <= 0.45f) { CloseFurniture(); return; }
            AddCorner(hit);
        }

        string Prompt() => _corners.Count == 0 ? "Walk the free floor. To mark furniture, look at one of its floor corners and hold still"
            : _corners.Count < 3 ? $"{_corners.Count} corner(s). Next corner of the furniture"
            : $"{_corners.Count} corners. Next corner, or look at the first to finish this piece";

        void AddCorner(Vector3 hit)
        {
            _corners.Add(new Vector2(hit.x, hit.z));
            var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(m.GetComponent<Collider>());
            m.transform.position = hit + Vector3.up * 0.03f;
            m.transform.localScale = Vector3.one * 0.1f;
            m.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(1f, 0.35f, 0.2f) };
            _markers.Add(m);

            _line.positionCount = _corners.Count;
            for (int i = 0; i < _corners.Count; i++) _line.SetPosition(i, new Vector3(_corners[i].x, _floorY + 0.03f, _corners[i].y));
        }

        void CloseFurniture()
        {
            int cells = _grid.MarkBlocked(_corners);
            TelemetryLogger.Info("Floor", $"furniture marked: {_corners.Count} corners, {cells} squares blocked");
            _corners.Clear();
            ClearMarkers();
            _dirty = true;
            _nextPaint = 0f;
        }

        void ClearMarkers()
        {
            foreach (var m in _markers) if (m != null) Destroy(m);
            _markers.Clear();
            if (_line != null) _line.positionCount = 0;
        }

        void Finish()
        {
            float free = _grid.AreaOf(FloorCell.Free), blocked = _grid.AreaOf(FloorCell.Blocked), unknown = _grid.AreaOf(FloorCell.Unknown);
            TelemetryLogger.Info("Floor", $"floor scan done: \"{_name}\" free {free:0.0} m2, furniture {blocked:0.0} m2, not checked {unknown:0.0} m2");
            Save();
            Teardown();
        }

        void Save()
        {
            try
            {
                var path = Path.Combine(Application.persistentDataPath, "floor-grids.json");
                var store = File.Exists(path) ? JsonUtility.FromJson<Store>(File.ReadAllText(path)) : new Store();
                if (store == null || store.rooms == null) store = new Store();

                store.rooms.RemoveAll(r => r.name == _name);
                store.rooms.Add(new Saved
                {
                    name = _name, x = _grid.Bounds.xMin, z = _grid.Bounds.yMin, cell = _grid.CellSize,
                    columns = _grid.Columns, rows = _grid.Rows, cells = _grid.Encode()
                });
                File.WriteAllText(path, JsonUtility.ToJson(store));
            }
            catch (System.Exception e)
            {
                TelemetryLogger.Info("Floor", "could not save the floor grid: " + e.Message);
            }
        }

        [System.Serializable] class Store { public List<Saved> rooms = new List<Saved>(); }

        [System.Serializable]
        class Saved
        {
            public string name;
            public float x, z, cell;
            public int columns, rows;
            public string cells;
        }

        void Cancel()
        {
            TelemetryLogger.Info("Floor", "floor scan cancelled on request");
            Teardown();
        }

        void Teardown()
        {
            _active = false;
            ClearMarkers();
            if (_quad != null) Destroy(_quad);
            if (_reticle != null) Destroy(_reticle.gameObject);
            if (_line != null) Destroy(_line.gameObject);
            if (_help != null) Destroy(_help.gameObject);
            if (_texture != null) Destroy(_texture);
        }

        void Build()
        {
            _texture = new Texture2D(_grid.Columns, _grid.Rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(_quad.GetComponent<Collider>());
            _quad.name = "FloorGrid";
            _quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _quad.transform.position = new Vector3(_grid.Bounds.center.x, _floorY + 0.015f, _grid.Bounds.center.y);
            _quad.transform.localScale = new Vector3(_grid.Columns * _grid.CellSize, _grid.Rows * _grid.CellSize, 1f);
            var mr = _quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = _texture };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Paint();

            var r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(r.GetComponent<Collider>());
            r.name = "FloorReticle";
            r.transform.localScale = Vector3.one * 0.08f;
            r.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = Color.white };
            _reticle = r.transform;

            var lineGo = new GameObject("FloorFurnitureOutline");
            _line = lineGo.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.startWidth = _line.endWidth = 0.03f;
            _line.positionCount = 0;
            if (_lineMaterial != null) _line.sharedMaterial = _lineMaterial;
            _line.startColor = _line.endColor = new Color(1f, 0.35f, 0.2f, 0.95f);

            var helpGo = new GameObject("FloorHelp");
            _help = helpGo.AddComponent<TextMesh>();
            _help.anchor = TextAnchor.MiddleCenter;
            _help.characterSize = 0.025f;
            _help.fontSize = 64;
            _help.color = Color.white;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                _help.font = font;
                helpGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        void Paint()
        {
            var px = new Color32[_grid.Columns * _grid.Rows];
            for (int r = 0; r < _grid.Rows; r++)
            for (int c = 0; c < _grid.Columns; c++)
            {
                var s = _grid[c, r];
                px[r * _grid.Columns + c] = s == FloorCell.Free ? new Color32(60, 220, 110, 90)
                    : s == FloorCell.Blocked ? new Color32(235, 70, 50, 170)
                    : new Color32(200, 200, 210, 22);
            }

            _texture.SetPixels32(px);
            _texture.Apply(false);
        }

        void SetHelp(Vector3 at, string text)
        {
            if (_help == null) return;
            _help.text = text;
            _help.transform.position = at;
            _help.transform.rotation = Quaternion.LookRotation(at - _head.position, Vector3.up);
        }

        void Poll()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.5f;

            var dir = Application.persistentDataPath;
            var cancel = Path.Combine(dir, "floor-cancel.request");
            if (File.Exists(cancel))
            {
                try { File.Delete(cancel); } catch { }
                if (_active) Cancel();
            }

            var done = Path.Combine(dir, "floor-done.request");
            if (File.Exists(done))
            {
                try { File.Delete(done); } catch { }
                if (_active) Finish();
            }

            var start = Path.Combine(dir, "floor-scan.request");
            if (File.Exists(start))
            {
                string name = string.Empty;
                try { name = File.ReadAllText(start).Trim(); File.Delete(start); } catch { }
                Begin(name);
            }
        }
    }
}

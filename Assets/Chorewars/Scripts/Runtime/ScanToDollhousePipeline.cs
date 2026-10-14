using System.Collections.Generic;
using Chorewars.AR;
using Chorewars.Dollhouse;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Builds a scaled dollhouse miniature from scanned rooms and keeps it in sync with coverage.
    ///
    /// Deliberately agnostic about where the rooms came from (fake or Quest): it consumes an
    /// <see cref="IScanSource"/>, produces placeholder room geometry, registers each room with the
    /// existing <see cref="DollhouseManager"/>, and pushes per-room coverage from the
    /// <see cref="CoverageMap"/> so the miniature tints as the floor is cleaned.
    /// </summary>
    public sealed class ScanToDollhousePipeline : MonoBehaviour
    {
        [SerializeField] private DollhouseManager dollhouseManager;
        [SerializeField] private CoverageMap coverageMap;
        [SerializeField] private float coveragePushIntervalSeconds = 0.25f;

        private readonly List<RoomScan> _rooms = new List<RoomScan>();
        private readonly Dictionary<string, GameObject> _roomGeometry = new Dictionary<string, GameObject>();

        private IScanSource _source;
        private Transform _miniatureRoot;
        private float _scale = 0.05f;
        private float _nextPushAt;
        private Material _wallMaterial;

        /// <summary>Rooms emitted by the scan so far.</summary>
        public int RoomCount => _rooms.Count;

        /// <summary>Room GameObjects created inside the miniature.</summary>
        public int MiniatureRoomCount => _roomGeometry.Count;

        public bool ScanComplete { get; private set; }

        /// <summary>Latest per-room coverage percentages, keyed by room id (diagnostics).</summary>
        public IReadOnlyDictionary<string, float> RoomCoverage => _roomCoverage;
        private readonly Dictionary<string, float> _roomCoverage = new Dictionary<string, float>();

        public void Configure(DollhouseManager manager, CoverageMap map)
        {
            dollhouseManager = manager;
            coverageMap = map;
        }

        public void StartScan(IScanSource source)
        {
            StopScan();

            _source = source;
            if (_source == null)
            {
                Debug.LogWarning("[ScanToDollhouse] no scan source; nothing will be built.");
                return;
            }

            _rooms.Clear();
            _roomGeometry.Clear();
            _roomCoverage.Clear();
            ScanComplete = false;

            PrepareMiniature();

            _source.RoomScanned += OnRoomScanned;
            _source.ScanCompleted += OnScanCompleted;
            _source.Begin();

            Debug.Log("[ScanToDollhouse] scan started.");
        }

        public void StopScan()
        {
            if (_source == null) return;

            _source.RoomScanned -= OnRoomScanned;
            _source.ScanCompleted -= OnScanCompleted;
            _source.Stop();
            _source = null;
        }

        private void PrepareMiniature()
        {
            if (dollhouseManager == null)
            {
                Debug.LogWarning("[ScanToDollhouse] no DollhouseManager; nothing will be built.");
                return;
            }

            // Derive the miniature scale from the manager's public world -> dollhouse mapping,
            // so we never need access to its private serialized fields.
            var origin = dollhouseManager.WorldToDollhouse(Vector3.zero);
            _scale = (dollhouseManager.WorldToDollhouse(Vector3.right) - origin).magnitude;
            if (_scale <= 0.0001f) _scale = 0.05f;

            var rootGo = new GameObject("DollhouseMiniature");
            rootGo.transform.SetParent(dollhouseManager.transform, false);
            _miniatureRoot = rootGo.transform;
        }

        private void OnRoomScanned(RoomScan room)
        {
            _rooms.Add(room);

            if (dollhouseManager == null || _miniatureRoot == null) return;
            if (_roomGeometry.ContainsKey(room.roomId)) return;

            var geometry = BuildRoom(room, _miniatureRoot);
            _roomGeometry[room.roomId] = geometry;

            var renderers = geometry.GetComponentsInChildren<Renderer>();
            dollhouseManager.RegisterRoom(room.roomId, renderers);

            Debug.Log($"[ScanToDollhouse] {room.roomId}: {room.boundsXZ.width:0.0}x{room.boundsXZ.height:0.0} m " +
                      $"-> {renderers.Length} renderers (rooms so far: {_rooms.Count})");
        }

        private void OnScanCompleted()
        {
            ScanComplete = true;
            Debug.Log($"[ScanToDollhouse] scan complete: {_rooms.Count} rooms, {_roomGeometry.Count} miniature rooms.");
        }

        private GameObject BuildRoom(RoomScan room, Transform parent)
        {
            var group = new GameObject($"Mini-{room.roomId}");
            group.transform.SetParent(parent, false);

            // Map the room's world XZ rectangle into miniature space via the public mapping.
            var min = dollhouseManager.WorldToDollhouse(new Vector3(room.boundsXZ.xMin, room.floorY, room.boundsXZ.yMin));
            var max = dollhouseManager.WorldToDollhouse(new Vector3(room.boundsXZ.xMax, room.floorY, room.boundsXZ.yMax));

            float width = Mathf.Max(0.005f, Mathf.Abs(max.x - min.x));
            float depth = Mathf.Max(0.005f, Mathf.Abs(max.z - min.z));
            float wallHeight = Mathf.Max(0.002f, room.wallHeight * _scale);
            var centre = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(group.transform, false);
            floor.transform.position = centre;
            floor.transform.localScale = new Vector3(width, Mathf.Max(0.001f, wallHeight * 0.15f), depth);
            StripCollider(floor);

            // One shared wall material for the whole miniature (created lazily) so the walls batch
            // instead of every room carrying its own material instance.
            _wallMaterial ??= PlaceholderMaterials.CreateTransparent(new Color(1f, 1f, 1f), 0.35f);
            var wallMaterial = _wallMaterial;
            float thickness = Mathf.Max(0.001f, wallHeight * 0.12f);
            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;

            MakeWall(group.transform, centre + new Vector3(-halfW, wallHeight * 0.5f, 0f), new Vector3(thickness, wallHeight, depth), wallMaterial);
            MakeWall(group.transform, centre + new Vector3(halfW, wallHeight * 0.5f, 0f), new Vector3(thickness, wallHeight, depth), wallMaterial);
            MakeWall(group.transform, centre + new Vector3(0f, wallHeight * 0.5f, -halfD), new Vector3(width, wallHeight, thickness), wallMaterial);
            MakeWall(group.transform, centre + new Vector3(0f, wallHeight * 0.5f, halfD), new Vector3(width, wallHeight, thickness), wallMaterial);

            return group;
        }

        private static void MakeWall(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            StripCollider(wall);

            var renderer = wall.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private static void StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private void Update()
        {
            if (dollhouseManager == null || coverageMap == null || _rooms.Count == 0) return;
            if (Time.unscaledTime < _nextPushAt) return;

            _nextPushAt = Time.unscaledTime + Mathf.Max(0.05f, coveragePushIntervalSeconds);

            for (int i = 0; i < _rooms.Count; i++)
            {
                var room = _rooms[i];
                float percent = coverageMap.ComputeCoveragePercent(room.boundsXZ);
                _roomCoverage[room.roomId] = percent;
                dollhouseManager.OnCoverageUpdate(room.roomId, percent);
            }
        }
    }
}

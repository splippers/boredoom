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
        private CoverageFloorVisualizer _floorVisualizer;
        private readonly Dictionary<string, PhotoFloor> _photoFloors = new Dictionary<string, PhotoFloor>();

        /// <summary>
        /// A room floor pointed at the live floor texture, plus what it was last set up for, so the
        /// mapping is only recomputed when the texture or the play area actually changes.
        /// </summary>
        private sealed class PhotoFloor
        {
            public Renderer Floor;
            public Material Material;
            public Vector3[] LocalXZ;
            public Vector2[] Uv;
            public Rect RoomRect;
            public int[] RoomCorner;
            public Vector2[] Targets;
            public bool Assigned;
            public Texture AppliedTexture;
            public Rect AppliedPlayArea;
        }

        /// <summary>Rooms emitted by the scan so far.</summary>
        public int RoomCount => _rooms.Count;

        /// <summary>The scanned rooms, in the order they arrived. Read-only: the list is live.</summary>
        public IReadOnlyList<RoomScan> Rooms => _rooms;

        /// <summary>Room GameObjects created inside the miniature.</summary>
        public int MiniatureRoomCount => _roomGeometry.Count;

        public bool ScanComplete { get; private set; }

        /// <summary>
        /// The world-space extent of everything the scan found, or null if it found nothing yet.
        /// This is the honest play area once a scan exists: before it, the only extent anyone can
        /// offer is a guess, and on device a guess is all there is until the scan runs.
        /// </summary>
        public Bounds? ScannedBounds
        {
            get
            {
                if (_rooms.Count == 0) return null;

                var bounds = _rooms[0].ToBounds();
                for (int i = 1; i < _rooms.Count; i++) bounds.Encapsulate(_rooms[i].ToBounds());
                return bounds;
            }
        }

        /// <summary>Latest per-room coverage percentages, keyed by room id (diagnostics).</summary>
        public IReadOnlyDictionary<string, float> RoomCoverage => _roomCoverage;
        private readonly Dictionary<string, float> _roomCoverage = new Dictionary<string, float>();

        public void Configure(DollhouseManager manager, CoverageMap map, CoverageFloorVisualizer floorVisualizer = null)
        {
            dollhouseManager = manager;
            coverageMap = map;
            _floorVisualizer = floorVisualizer;
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
            ClearFloorTextures();
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

            // Only the floor takes the clean/dirty colour. The walls are a translucent shell you are
            // meant to see the miniature through, and restyling them with the opaque clean/dirty
            // material hid everything inside the room.
            var floorRenderer = geometry.transform.Find("Floor")?.GetComponent<Renderer>();
            dollhouseManager.RegisterRoom(
                room.roomId,
                renderers,
                floorRenderer != null ? new[] { floorRenderer } : renderers);

            Debug.Log($"[ScanToDollhouse] {room.roomId}: {room.boundsXZ.width:0.0}x{room.boundsXZ.height:0.0} m " +
                      $"-> {renderers.Length} renderers (rooms so far: {_rooms.Count})");

            if (floorRenderer != null) AttachFloorTexture(room, floorRenderer);
        }

        /// <summary>
        /// Points a room's floor at the live floor texture, so the miniature shows the photographs the
        /// room does instead of a copy of them. Only the mapping differs per room.
        /// </summary>
        private void AttachFloorTexture(RoomScan room, Renderer floorRenderer)
        {
            if (dollhouseManager == null) return;

            var material = DollhouseFloorTexture.CreateMaterial(null);
            if (material == null) return;

            if (!DollhouseFloorTexture.TryReadFloorFace(floorRenderer, out var localXZ, out var uv))
            {
                Debug.LogWarning($"[ScanToDollhouse] {room.roomId}: no readable top face on the floor; it keeps its tint.");
                Destroy(material);
                return;
            }

            // Which corner of the room each corner of the floor stands for. The miniature is drawn at
            // the dollhouse, nowhere near the room, so this compares against where the room's corners
            // are drawn rather than where they really are.
            var roomCorners = new Vector3[4];
            for (int k = 0; k < 4; k++)
            {
                var corner = RoomCorner(room.boundsXZ, k);
                roomCorners[k] = dollhouseManager.WorldToDollhouse(new Vector3(corner.x, room.floorY, corner.y));
            }

            float tolerance = Vector3.Distance(roomCorners[0], roomCorners[2]) * 0.25f;
            var corners = new int[localXZ.Length];
            float worst = 0f;

            for (int i = 0; i < localXZ.Length; i++)
            {
                var world = floorRenderer.transform.TransformPoint(localXZ[i]);
                float best = float.MaxValue;
                int bestCorner = 0;

                for (int k = 0; k < 4; k++)
                {
                    float d = Vector2.Distance(new Vector2(world.x, world.z), new Vector2(roomCorners[k].x, roomCorners[k].z));
                    if (d >= best) continue;

                    best = d;
                    bestCorner = k;
                }

                corners[i] = bestCorner;
                worst = Mathf.Max(worst, best);
            }

            if (worst > tolerance)
            {
                Debug.LogWarning($"[ScanToDollhouse] {room.roomId}: the floor does not sit on the room's corners " +
                                 $"({worst:0.000} m off); it keeps its tint.");
                Destroy(material);
                return;
            }

            _photoFloors[room.roomId] = new PhotoFloor
            {
                Floor = floorRenderer,
                Material = material,
                LocalXZ = localXZ,
                Uv = uv,
                RoomRect = room.boundsXZ,
                RoomCorner = corners,
                Targets = new Vector2[corners.Length],
            };
        }

        private void ClearFloorTextures()
        {
            foreach (var pair in _photoFloors)
            {
                if (pair.Value.Material != null) Destroy(pair.Value.Material);
            }

            _photoFloors.Clear();
        }

        /// <summary>
        /// Keeps every room floor reading the right part of the live texture. This cannot be a one-off
        /// at build time: the texture is rebuilt when the play area is re-derived from the scan, which
        /// happens after the rooms are built.
        /// </summary>
        private void PushFloorTexture()
        {
            if (_floorVisualizer == null || _photoFloors.Count == 0) return;

            var texture = _floorVisualizer.Texture;
            if (texture == null) return;

            var bounds = _floorVisualizer.Bounds;
            var playArea = new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z);
            if (playArea.width <= 0f || playArea.height <= 0f) return;

            foreach (var pair in _photoFloors)
            {
                var photo = pair.Value;
                if (photo.Material == null || photo.Floor == null) continue;
                if (photo.AppliedTexture == texture && photo.AppliedPlayArea == playArea) continue;

                for (int i = 0; i < photo.Targets.Length; i++)
                {
                    var corner = RoomCorner(photo.RoomRect, photo.RoomCorner[i]);
                    photo.Targets[i] = new Vector2(
                        (corner.x - playArea.xMin) / playArea.width,
                        (corner.y - playArea.yMin) / playArea.height);
                }

                if (!DollhouseFloorTexture.TrySolve(photo.Uv, photo.Targets, out var scale, out var offset))
                {
                    Debug.LogWarning($"[ScanToDollhouse] {pair.Key}: the floor's UVs do not map onto the room's " +
                                     "rectangle; it keeps its tint.");

                    if (!photo.Assigned)
                    {
                        Destroy(photo.Material);
                        photo.Material = null;
                    }

                    continue;
                }

                // Taking the floor over only once there is a mapping to put on it, so a room that
                // cannot be mapped keeps its clean/dirty tint rather than turning blank.
                if (!photo.Assigned)
                {
                    photo.Floor.sharedMaterial = photo.Material;
                    dollhouseManager.MarkUntinted(photo.Floor);
                    photo.Assigned = true;
                }

                photo.Material.SetTexture(FloorTextureId, texture);
                photo.Material.mainTextureScale = scale;
                photo.Material.mainTextureOffset = offset;
                photo.AppliedTexture = texture;
                photo.AppliedPlayArea = playArea;
            }
        }

        /// <summary>
        /// The room's rectangle corner at <paramref name="corner"/>, numbered anticlockwise from the
        /// near-left one.
        /// </summary>
        private static Vector2 RoomCorner(Rect room, int corner)
        {
            float x = (corner == 0 || corner == 3) ? room.xMin : room.xMax;
            float z = (corner < 2) ? room.yMin : room.yMax;
            return new Vector2(x, z);
        }

        private static readonly int FloorTextureId = Shader.PropertyToID("_MainTex");

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

            PushFloorTexture();
        }
    }
}

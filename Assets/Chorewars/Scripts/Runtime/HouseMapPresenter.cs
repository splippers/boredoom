using System.Collections.Generic;
using Chorewars.Dollhouse;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Draws what the player has said about the house onto the miniature: a name floating over each
    /// room, a doorway arch where they walked between two rooms, and a line joining rooms that
    /// connect.
    /// </summary>
    /// <remarks>
    /// Presentation only. It reads <see cref="RoomScan"/>/<see cref="RoomName"/>/<see cref="RoomLink"/>
    /// and the dollhouse mapping, and it never decides anything: if it is switched off the model still
    /// holds the names and the doors, they are just not drawn.
    ///
    /// Labels billboard toward the camera so they stay readable from any angle, which for a headset is
    /// the only angle that matters. Everything is translucent and built from primitives; no assets.
    /// </remarks>
    public sealed class HouseMapPresenter : MonoBehaviour
    {
        [SerializeField] private Color labelColour = new Color(0.95f, 0.98f, 1f, 0.95f);
        [SerializeField] private Color doorwayColour = new Color(0.35f, 0.85f, 1f, 0.75f);
        [SerializeField] private Color linkColour = new Color(0.35f, 0.85f, 1f, 0.35f);

        [Header("Labels")]
        [SerializeField] private float labelHeightMeters = 0.09f;
        [SerializeField] private float labelCharacterSize = 0.006f;
        [SerializeField] private int labelFontSize = 72;

        [Header("Doorways")]
        [SerializeField] private float postThicknessMeters = 0.008f;
        [SerializeField] private float lintelHeightMeters = 0.012f;
        [SerializeField] private float linkLineWidthMeters = 0.006f;

        private DollhouseManager _dollhouse;
        private Camera _camera;
        private Transform _root;

        private readonly Dictionary<string, RoomScan> _rooms = new Dictionary<string, RoomScan>();
        private readonly Dictionary<string, TextMesh> _labels = new Dictionary<string, TextMesh>();
        private readonly Dictionary<string, GameObject> _labelsGo = new Dictionary<string, GameObject>();

        private GameObject _linksRoot;
        private GameObject _doorsRoot;
        private Material _doorwayMaterial;
        private Material _linkMaterial;

        public void Configure(DollhouseManager dollhouse, Camera camera, Transform root = null)
        {
            _dollhouse = dollhouse;
            _camera = camera;
            _root = root;
        }

        /// <summary>Room count that has been given a label, for diagnostics and probes.</summary>
        public int LabelledRoomCount => _labels.Count;

        /// <summary>Doorways currently drawn.</summary>
        public int DoorwayCount => _doorsRoot != null ? _doorsRoot.transform.childCount : 0;

        /// <summary>Link lines currently drawn.</summary>
        public int LinkCount => _linksRoot != null ? _linksRoot.transform.childCount : 0;

        /// <summary>
        /// Adds (or re-adds) a room. A room that changes bounds keeps its label but moves it, so a
        /// rescan does not leave a name hovering over empty floor.
        /// </summary>
        public void SetRoom(RoomScan room)
        {
            if (string.IsNullOrEmpty(room.roomId)) return;

            _rooms[room.roomId] = room;
            EnsureLabel(room.roomId);
            PlaceLabel(room);
        }

        /// <summary>Sets the name shown over a room. An empty name hides the label.</summary>
        public void SetName(string roomId, string name)
        {
            if (!_labels.TryGetValue(roomId, out var label) || label == null) return;

            if (string.IsNullOrEmpty(name))
            {
                label.text = string.Empty;
                if (_labelsGo.TryGetValue(roomId, out var go) && go != null) go.SetActive(false);
                return;
            }

            label.text = name;
            label.color = labelColour;

            if (_labelsGo.TryGetValue(roomId, out var labelGo) && labelGo != null) labelGo.SetActive(true);
        }

        /// <summary>
        /// Redraws the doorways and the lines joining linked rooms. Rebuilt as a whole rather than
        /// patched, because a walk can add a door anywhere and there are only ever a handful of rooms.
        /// </summary>
        public void SetLinks(IReadOnlyList<RoomLink> links)
        {
            EnsureVisualRoots();
            if (_linksRoot == null) return;

            ClearChildren(_doorsRoot);
            ClearChildren(_linksRoot);

            if (links == null) return;

            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (!link.IsValid) continue;

                DrawDoorway(link);
                DrawLink(link);
            }
        }

        private void EnsureLabel(string roomId)
        {
            if (_labels.ContainsKey(roomId)) return;

            var go = new GameObject($"Label-{roomId}");
            go.transform.SetParent(_root != null ? _root : transform, false);

            var text = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.font = font;
            text.fontSize = labelFontSize;
            text.characterSize = labelCharacterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = labelColour;
            text.text = string.Empty;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (font != null) renderer.sharedMaterial = font.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            go.SetActive(false);
            _labels[roomId] = text;
            _labelsGo[roomId] = go;
        }

        private void PlaceLabel(RoomScan room)
        {
            if (_labelsGo.TryGetValue(room.roomId, out var go) && go != null)
                go.transform.position = LabelWorldPosition(room);
        }

        /// <summary>Above the room's centre, in the miniature, high enough to clear the walls.</summary>
        private Vector3 LabelWorldPosition(RoomScan room)
        {
            var centre = new Vector3(room.boundsXZ.center.x, room.floorY, room.boundsXZ.center.y);
            var mapped = _dollhouse != null ? _dollhouse.WorldToDollhouse(centre) : centre;

            float scale = 1f;
            if (_dollhouse != null)
            {
                var origin = _dollhouse.WorldToDollhouse(Vector3.zero);
                scale = (_dollhouse.WorldToDollhouse(Vector3.right) - origin).magnitude;
                if (scale <= 0.0001f) scale = 0.05f;
            }

            // The label hangs above the room's walls, plus a fixed lift so several room labels at the
            // same height do not sit on top of one another.
            mapped.y += room.wallHeight * scale + labelHeightMeters;
            return mapped;
        }

        private void EnsureVisualRoots()
        {
            var parent = _root != null ? _root : transform;

            if (_doorsRoot == null)
            {
                _doorsRoot = new GameObject("Doorways");
                _doorsRoot.transform.SetParent(parent, false);
            }

            if (_linksRoot == null)
            {
                _linksRoot = new GameObject("RoomLinks");
                _linksRoot.transform.SetParent(parent, false);
            }

            _doorwayMaterial ??= MakeMaterial(doorwayColour);
            _linkMaterial ??= MakeMaterial(linkColour);
        }

        private static Material MakeMaterial(Color colour)
        {
            var material = PlaceholderMaterials.CreateTransparent(colour, colour.a);
            material.name = "HouseMapTransparent";
            return material;
        }

        /// <summary>
        /// Stands an arch on the wall the player walked through: two posts either side of the opening
        /// and a lintel across the top, so a solid wall still shows where the door is.
        /// </summary>
        /// <remarks>
        /// Drawn once for the link, not once per room. Both rooms of a link measure the same crossing
        /// and so put their opening on the same shared wall in the same place; drawing it from both
        /// would only stack two identical arches. The room on the near side of the walk gives the
        /// geometry, and the far room is the fallback for the odd link that was assembled by hand.
        /// </remarks>
        private void DrawDoorway(RoomLink link)
        {
            if (_rooms.TryGetValue(link.fromRoomId, out var from) &&
                DrawArchForRoom(from, link.crossingPoint))
                return;

            if (_rooms.TryGetValue(link.toRoomId, out var to))
                DrawArchForRoom(to, link.crossingPoint);
        }

        private bool DrawArchForRoom(RoomScan room, Vector3 crossing)
        {
            if (_doorsRoot == null || _dollhouse == null) return false;

            var crossingXZ = new Vector2(crossing.x, crossing.z);
            if (!HouseDoorway.TryFind(room.boundsXZ, crossingXZ, out var gap)) return false;

            float scale = MiniatureScale();
            float postHeight = Mathf.Max(0.01f, room.wallHeight * scale);
            float thickness = Mathf.Max(0.002f, postThicknessMeters);

            // Two posts either side of the opening and a lintel across the top. The miniature walls are
            // a translucent shell, so a bright arch reads as the doorway far better than a hole in a
            // wall you can already see through, and it marks exactly where the player walked.
            MakePost(gap, room, gap.gapMin, postHeight, thickness);
            MakePost(gap, room, gap.gapMax, postHeight, thickness);

            var lintelWorld = gap.alongX
                ? new Vector3(gap.GapCentre, room.floorY, gap.offset)
                : new Vector3(gap.offset, room.floorY, gap.GapCentre);
            var lintelMapped = _dollhouse.WorldToDollhouse(lintelWorld);

            float lintelWidth = Mathf.Max(0.01f, gap.GapWidth * scale) + thickness * 2f;

            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = $"DoorLintel-{room.roomId}";
            lintel.transform.SetParent(_doorsRoot.transform, false);
            lintel.transform.position = lintelMapped + Vector3.up * postHeight;
            lintel.transform.localScale = gap.alongX
                ? new Vector3(lintelWidth, lintelHeightMeters, thickness)
                : new Vector3(thickness, lintelHeightMeters, lintelWidth);
            StripCollider(lintel);
            Paint(lintel, _doorwayMaterial);

            return true;
        }

        private void MakePost(WallGap gap, RoomScan room, float at, float postHeight, float thickness)
        {
            if (_doorsRoot == null || _dollhouse == null) return;

            var world = gap.alongX
                ? new Vector3(at, room.floorY, gap.offset)
                : new Vector3(gap.offset, room.floorY, at);
            var mapped = _dollhouse.WorldToDollhouse(world);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"DoorPost-{room.roomId}";
            go.transform.SetParent(_doorsRoot.transform, false);
            go.transform.position = mapped + Vector3.up * (postHeight * 0.5f);
            go.transform.localScale = new Vector3(thickness, postHeight, thickness);
            StripCollider(go);
            Paint(go, _doorwayMaterial);
        }

        /// <summary>A translucent line joining the two rooms of a link, over the top of the boxes.</summary>
        private void DrawLink(RoomLink link)
        {
            if (_linksRoot == null || _dollhouse == null) return;
            if (!_rooms.TryGetValue(link.fromRoomId, out var from)) return;
            if (!_rooms.TryGetValue(link.toRoomId, out var to)) return;

            var a = LinkAnchor(from);
            var b = LinkAnchor(to);

            var go = new GameObject($"Link-{link.fromRoomId}-{link.toRoomId}");
            go.transform.SetParent(_linksRoot.transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.startWidth = linkLineWidthMeters;
            line.endWidth = linkLineWidthMeters;
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = _linkMaterial;
        }

        /// <summary>Where a link springs from: above the room's centre, clear of the walls.</summary>
        private Vector3 LinkAnchor(RoomScan room)
        {
            var centre = new Vector3(room.boundsXZ.center.x, room.floorY, room.boundsXZ.center.y);
            var mapped = _dollhouse.WorldToDollhouse(centre);
            mapped.y += room.wallHeight * MiniatureScale() * 1.2f;
            return mapped;
        }

        private float MiniatureScale()
        {
            if (_dollhouse == null) return 0.05f;

            var origin = _dollhouse.WorldToDollhouse(Vector3.zero);
            float scale = (_dollhouse.WorldToDollhouse(Vector3.right) - origin).magnitude;
            return scale > 0.0001f ? scale : 0.05f;
        }

        private static void Paint(GameObject go, Material material)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
        }

        private static void StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) DestroyNow(collider);
        }

        /// <summary>
        /// Removes an object the presenter created. In play mode <c>Destroy</c> is deferred to the end
        /// of the frame; in edit mode it is not allowed at all, which is what makes the dressings
        /// testable. The caller detaches first where the count matters immediately.
        /// </summary>
        private static void DestroyNow(Object go)
        {
            if (go == null) return;

            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private static void ClearChildren(GameObject root)
        {
            if (root == null) return;

            // Detached before being destroyed so the child count is correct this instant: Destroy is
            // deferred, and a redraw that counted its predecessor's leftovers would draw them twice.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i);
                child.SetParent(null, false);
                DestroyNow(child.gameObject);
            }
        }

        /// <summary>Turn every label to face the player. A label you cannot read is not a label.</summary>
        private void LateUpdate()
        {
            if (_camera == null) return;

            var facing = _camera.transform.rotation;

            foreach (var pair in _labelsGo)
            {
                var go = pair.Value;
                if (go == null || !go.activeSelf) continue;
                go.transform.rotation = facing;
            }
        }
    }
}

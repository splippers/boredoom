using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Draws each room the house map knows, in the real room, as glowing lines: a loop on the floor, a loop at the
    /// top of the walls and a post at each corner. The room the player stands in is drawn brighter and thicker.
    /// </summary>
    public sealed class RoomOutlines : MonoBehaviour
    {
        const float RefreshSeconds = 0.4f;
        const float FloorLift = 0.02f;

        HouseMapDriver _driver;
        Transform _head;
        Material _material;
        float _nextAt;
        readonly Dictionary<string, Outline> _outlines = new Dictionary<string, Outline>();

        sealed class Outline
        {
            public GameObject root;
            public LineRenderer floor, top;
            public List<LineRenderer> posts;
            public TextMesh label;
            public Rect rect;
            public Vector2[] corners;
            public float wallHeight, floorY;
            public Color colour;
        }

        public void Configure(HouseMapDriver driver, Transform head)
        {
            _driver = driver;
            _head = head;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) _material = new Material(shader);
        }

        /// <summary>The four floor corners of a room, in order round the edge.</summary>
        public static Vector3[] Corners(Rect r, float y) => new[]
        {
            new Vector3(r.xMin, y, r.yMin), new Vector3(r.xMax, y, r.yMin),
            new Vector3(r.xMax, y, r.yMax), new Vector3(r.xMin, y, r.yMax)
        };

        void Update()
        {
            if (_driver == null || Time.unscaledTime < _nextAt) return;
            _nextAt = Time.unscaledTime + RefreshSeconds;

            var rooms = _driver.Rooms;
            string here = _head != null ? HouseMapBuilder.FindRoomIdAt(rooms, _head.position) : null;

            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (!_outlines.TryGetValue(room.roomId, out var o))
                {
                    o = Make(room.roomId, i);
                    _outlines[room.roomId] = o;
                }

                bool current = room.roomId == here;
                if (o.rect != room.boundsXZ || !SameCorners(o.corners, room.FittedCorners()) || o.wallHeight != room.wallHeight || o.floorY != room.floorY) Shape(o, room);
                Style(o, current);
                o.label.text = _driver.NameOf(room.roomId);
            }
        }

        void LateUpdate()
        {
            if (_head == null) return;
            foreach (var o in _outlines.Values)
            {
                if (o.label == null || string.IsNullOrEmpty(o.label.text)) continue;
                o.label.transform.rotation = Quaternion.LookRotation(o.label.transform.position - _head.position, Vector3.up);
            }
        }

        Outline Make(string id, int index)
        {
            var o = new Outline { root = new GameObject("Outline-" + id), colour = Color.HSVToRGB((index * 0.19f + 0.52f) % 1f, 0.55f, 1f) };
            o.root.transform.SetParent(transform, false);
            o.floor = Line(o.root.transform, 5);
            o.top = Line(o.root.transform, 5);
            o.posts = new List<LineRenderer>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(o.root.transform, false);
            o.label = labelGo.AddComponent<TextMesh>();
            o.label.anchor = TextAnchor.MiddleCenter;
            o.label.characterSize = 0.04f;
            o.label.fontSize = 64;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                o.label.font = font;
                labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return o;
        }

        LineRenderer Line(Transform parent, int points)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<LineRenderer>();
            l.useWorldSpace = true;
            l.positionCount = points;
            l.shadowCastingMode = ShadowCastingMode.Off;
            l.receiveShadows = false;
            l.numCornerVertices = 2;
            if (_material != null) l.sharedMaterial = _material;
            return l;
        }

        static bool SameCorners(Vector2[] a, Vector2[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        void Shape(Outline o, RoomScan room)
        {
            o.rect = room.boundsXZ;
            o.corners = room.FittedCorners();
            o.wallHeight = room.wallHeight;
            o.floorY = room.floorY;

            float y0 = room.floorY + FloorLift;
            float y1 = room.floorY + Mathf.Max(1.8f, room.wallHeight);

            Vector3[] low, high;
            if (o.corners != null)
            {
                low = new Vector3[o.corners.Length];
                high = new Vector3[o.corners.Length];
                for (int i = 0; i < low.Length; i++)
                {
                    low[i] = new Vector3(o.corners[i].x, y0, o.corners[i].y);
                    high[i] = new Vector3(o.corners[i].x, y1, o.corners[i].y);
                }
            }
            else
            {
                low = Corners(room.boundsXZ, y0);
                high = Corners(room.boundsXZ, y1);
            }

            int n = low.Length;
            o.floor.positionCount = n + 1;
            o.top.positionCount = n + 1;
            for (int i = 0; i <= n; i++)
            {
                o.floor.SetPosition(i, low[i % n]);
                o.top.SetPosition(i, high[i % n]);
            }

            while (o.posts.Count < n) o.posts.Add(Line(o.root.transform, 2));
            for (int i = 0; i < o.posts.Count; i++)
            {
                bool used = i < n;
                o.posts[i].gameObject.SetActive(used);
                if (!used) continue;
                o.posts[i].SetPosition(0, low[i]);
                o.posts[i].SetPosition(1, high[i]);
            }

            var centre = new Vector3(room.boundsXZ.center.x, y1 - 0.25f, room.boundsXZ.center.y);
            o.label.transform.position = centre;
        }

        static void Style(Outline o, bool current)
        {
            var bright = current ? Color.Lerp(o.colour, Color.white, 0.5f) : o.colour;
            Set(o.floor, bright, current ? 0.07f : 0.04f, 1f);
            Set(o.top, bright, current ? 0.04f : 0.02f, current ? 0.8f : 0.45f);
            foreach (var p in o.posts) Set(p, bright, current ? 0.03f : 0.02f, current ? 0.6f : 0.3f);
            o.label.color = new Color(bright.r, bright.g, bright.b, 1f);
        }

        static void Set(LineRenderer l, Color c, float width, float alpha)
        {
            c.a = alpha;
            l.startColor = c;
            l.endColor = c;
            l.startWidth = width;
            l.endWidth = width;
        }
    }
}

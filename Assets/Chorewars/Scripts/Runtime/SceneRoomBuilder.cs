using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.Runtime
{
    public enum SceneSurfaceKind { Floor, Ceiling, Wall, Door, Window, Other }

    /// <summary>One flat surface from the room the player outlined in the headset's Space Setup, in world space.</summary>
    public struct SceneSurface
    {
        public SceneSurfaceKind kind;
        public Vector3 center;

        /// <summary>Corners seen from above (x, z). Only floors and ceilings need it.</summary>
        public Vector2[] outlineXZ;
    }

    /// <summary>The rooms and doorways read from the headset's saved room outline.</summary>
    public sealed class SceneRooms
    {
        public float floorY;
        public readonly List<RoomScan> rooms = new List<RoomScan>();
        public readonly List<RoomLink> doors = new List<RoomLink>();
    }

    /// <summary>
    /// Turns the headset's saved room outline (floor, ceiling and door-frame surfaces) into rooms and the
    /// doorways that link them. Pure maths, no engine services, so it is tested without a headset.
    /// </summary>
    /// <remarks>
    /// Every floor is a room. Two floors that mostly overlap are one room seen twice and are merged. A door frame
    /// that stands between two rooms is a doorway joining them, which is how rooms come out linked before the
    /// player has walked through anything.
    /// </remarks>
    public static class SceneRoomBuilder
    {
        public const string IdPrefix = "Scene-";
        public const float DefaultWallHeightMeters = 2.4f;
        public const float DoorReachMeters = 0.9f;

        /// <summary>Doorways from the latest scene read; the house map merges them with the ones the walk finds.</summary>
        public static readonly List<RoomLink> PublishedDoors = new List<RoomLink>();

        /// <summary>Rooms already issued an id this session or in the saved house, so a rescan gives them the same ids.</summary>
        public static readonly List<RoomScan> Known = new List<RoomScan>();

        /// <summary>Builds the rooms, keeping ids stable against <see cref="Known"/>, and remembers the result.</summary>
        public static SceneRooms BuildStable(IReadOnlyList<SceneSurface> surfaces)
        {
            var result = Build(surfaces, Known);
            Remember(result.rooms);
            return result;
        }

        /// <summary>Notes rooms (for example those loaded from the saved house) so their ids are never reused.</summary>
        public static void Remember(IEnumerable<RoomScan> rooms)
        {
            if (rooms == null) return;
            foreach (var room in rooms)
            {
                if (string.IsNullOrEmpty(room.roomId) || !room.roomId.StartsWith(IdPrefix) || room.assumed) continue;
                int at = Known.FindIndex(k => k.roomId == room.roomId);
                if (at >= 0) Known[at] = room; else Known.Add(room);
            }
        }

        public static SceneRooms Build(IReadOnlyList<SceneSurface> surfaces) => Build(surfaces, null);

        /// <summary>
        /// A room that overlaps a previously issued one (by at least half) takes its id, so ids no longer shuffle when
        /// room sizes change between scans. A new room takes the next number above every id issued before.
        /// </summary>
        public static SceneRooms Build(IReadOnlyList<SceneSurface> surfaces, IReadOnlyList<RoomScan> previous)
        {
            var result = new SceneRooms();
            if (surfaces == null) return result;

            var floors = new List<(Rect rect, float y, Vector2[] corners)>();
            var ceilings = new List<(Vector2 centre, float y)>();
            var doors = new List<Vector3>();
            float lowest = float.PositiveInfinity;

            for (int i = 0; i < surfaces.Count; i++)
            {
                var s = surfaces[i];
                switch (s.kind)
                {
                    case SceneSurfaceKind.Floor:
                        if (!TryRect(s, out var rect)) break;
                        floors.Add((rect, s.center.y, s.outlineXZ));
                        lowest = Mathf.Min(lowest, s.center.y);
                        break;
                    case SceneSurfaceKind.Ceiling:
                        ceilings.Add((new Vector2(s.center.x, s.center.z), s.center.y));
                        break;
                    case SceneSurfaceKind.Door:
                        doors.Add(s.center);
                        break;
                }
            }

            if (floors.Count == 0) return result;
            result.floorY = lowest;

            floors.Sort((a, b) => (b.rect.width * b.rect.height).CompareTo(a.rect.width * a.rect.height));
            var merged = new List<(Rect rect, float y, Vector2[] corners)>();
            foreach (var f in floors)
            {
                bool absorbed = false;
                for (int i = 0; i < merged.Count; i++)
                {
                    if (OverlapShare(merged[i].rect, f.rect) < 0.5f) continue;
                    // Two surfaces made one room: the union is no longer either one's outline, so only the box is kept.
                    merged[i] = (Union(merged[i].rect, f.rect), merged[i].y, null);
                    absorbed = true;
                    break;
                }
                if (!absorbed) merged.Add(f);
            }

            var ids = AssignIds(merged.ConvertAll(m => m.rect), previous);

            for (int i = 0; i < merged.Count; i++)
            {
                float height = DefaultWallHeightMeters;
                foreach (var c in ceilings)
                {
                    if (!Inflate(merged[i].rect, 0.3f).Contains(c.centre)) continue;
                    height = Mathf.Clamp(c.y - merged[i].y, 1.8f, 4f);
                    break;
                }

                result.rooms.Add(new RoomScan
                {
                    roomId = ids[i],
                    boundsXZ = merged[i].rect,
                    floorY = merged[i].y,
                    wallHeight = height,
                    cornersXZ = merged[i].corners != null && merged[i].corners.Length >= 3 ? merged[i].corners : null
                });
            }

            foreach (var door in doors)
            {
                var p = new Vector2(door.x, door.z);
                string first = null, second = null;
                float firstD = float.MaxValue, secondD = float.MaxValue;
                foreach (var room in result.rooms)
                {
                    float d = HouseMapBuilder.DistanceTo(room.boundsXZ, p);
                    if (d > DoorReachMeters) continue;
                    if (d < firstD) { second = first; secondD = firstD; first = room.roomId; firstD = d; }
                    else if (d < secondD) { second = room.roomId; secondD = d; }
                }

                if (first == null || second == null) continue;   // a door to the outside, or to a room never outlined

                var link = new RoomLink { fromRoomId = first, toRoomId = second, crossingPoint = new Vector3(door.x, result.floorY, door.z) };
                if (!HouseMapBuilder.RecordsDoor(result.doors, link)) result.doors.Add(link);
            }

            return result;
        }

        /// <summary>Replaces the doorways the house map will pick up.</summary>
        public static void Publish(IReadOnlyList<RoomLink> doors)
        {
            PublishedDoors.Clear();
            if (doors != null) PublishedDoors.AddRange(doors);
        }

        private static List<string> AssignIds(List<Rect> rects, IReadOnlyList<RoomScan> previous)
        {
            var ids = new List<string>(new string[rects.Count]);
            var taken = new HashSet<string>();
            int highest = 0;

            if (previous != null)
            {
                foreach (var p in previous)
                    if (p.roomId != null && p.roomId.StartsWith(IdPrefix) && int.TryParse(p.roomId.Substring(IdPrefix.Length), out int n))
                        highest = Mathf.Max(highest, n);

                // Best overlaps claim ids first, so two close rooms cannot swap.
                var pairs = new List<(float share, int room, int prev)>();
                for (int r = 0; r < rects.Count; r++)
                    for (int k = 0; k < previous.Count; k++)
                    {
                        if (previous[k].roomId == null || !previous[k].roomId.StartsWith(IdPrefix)) continue;
                        float share = OverlapShare(rects[r], previous[k].boundsXZ);
                        if (share >= 0.5f) pairs.Add((share, r, k));
                    }
                pairs.Sort((a, b) => b.share.CompareTo(a.share));
                foreach (var pr in pairs)
                {
                    string id = previous[pr.prev].roomId;
                    if (ids[pr.room] != null || taken.Contains(id)) continue;
                    ids[pr.room] = id;
                    taken.Add(id);
                }
            }

            for (int r = 0; r < ids.Count; r++)
                if (ids[r] == null) ids[r] = IdPrefix + (++highest);
            return ids;
        }

        private static bool TryRect(SceneSurface s, out Rect rect)
        {
            rect = default;
            if (s.outlineXZ == null || s.outlineXZ.Length < 3) return false;

            var min = s.outlineXZ[0];
            var max = s.outlineXZ[0];
            foreach (var p in s.outlineXZ)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            if (max.x - min.x < 0.5f || max.y - min.y < 0.5f) return false;   // a ledge or a rug, not a room
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private static float OverlapShare(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            if (w <= 0f || h <= 0f) return 0f;
            return w * h / Mathf.Min(a.width * a.height, b.width * b.height);
        }

        private static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        private static Rect Inflate(Rect r, float by) =>
            Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);
    }
}

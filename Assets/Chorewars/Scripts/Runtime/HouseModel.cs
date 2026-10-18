using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>A room the player has given a name of their own, rather than a scanned one.</summary>
    /// <remarks>
    /// The scanned rooms arrive as boxes with ids like "Room-001", which are no use to anybody who
    /// lives there. This is what the player said instead, kept beside the id rather than replacing it:
    /// the id is what the coverage map, the scoring and the dollhouse geometry are keyed on, and those
    /// must keep working whether or not anybody has said "this is the kitchen".
    /// </remarks>
    [Serializable]
    public struct RoomName
    {
        /// <summary>The id of the <see cref="RoomScan"/> this names.</summary>
        public string roomId;

        /// <summary>What the player called it, as they said it.</summary>
        public string name;

        /// <summary>When it was named, ISO-8601 UTC.</summary>
        public string namedAtUtc;

        public bool IsValid => !string.IsNullOrEmpty(roomId) && !string.IsNullOrEmpty(name);
    }

    /// <summary>
    /// A doorway: a place the player walked out of one room and into another.
    /// </summary>
    /// <remarks>
    /// This is a *link*, not a wall. Meshing cannot see a doorway and this does not need it to: a
    /// crossing is inferred from the head path, so what is recorded is where the player actually went
    /// through, not where a mesh happens to have a gap. Two samples in different rooms is a crossing,
    /// and the midpoint is where they passed the threshold.
    /// </remarks>
    [Serializable]
    public struct RoomLink
    {
        /// <summary>The room the player left.</summary>
        public string fromRoomId;

        /// <summary>The room the player entered.</summary>
        public string toRoomId;

        /// <summary>Where they crossed, in world space.</summary>
        public Vector3 crossingPoint;

        public bool IsValid => !string.IsNullOrEmpty(fromRoomId) && !string.IsNullOrEmpty(toRoomId) && fromRoomId != toRoomId;

        /// <summary>
        /// The same doorway whichever way it is walked, so "kitchen to hall" and "hall to kitchen" are
        /// one door and not two.
        /// </summary>
        public string PairKey => string.CompareOrdinal(fromRoomId, toRoomId) <= 0
            ? fromRoomId + "|" + toRoomId
            : toRoomId + "|" + fromRoomId;
    }

    /// <summary>
    /// The arithmetic behind "the player walked from the kitchen into the hall, so there is a door
    /// there". Rooms and a head path in; names and links out.
    /// </summary>
    /// <remarks>
    /// Nothing here touches the scene, a microphone or a MonoBehaviour, so it can be tested by feeding
    /// it rectangles and a straight line rather than by walking a headset around a kitchen. It also
    /// does not care where the rooms came from: a meshing scan, the play-space boundary and an assumed
    /// box are all just <see cref="RoomScan"/> rectangles by the time they get here.
    /// </remarks>
    public static class HouseMapBuilder
    {
        /// <summary>
        /// The names offered when the player would rather pick than talk. Short, distinct, and
        /// covering the rooms a house actually has.
        /// </summary>
        public static readonly string[] FallbackNames =
        {
            "Kitchen", "Lounge", "Hall", "Bedroom", "Bathroom", "Stairs", "Other",
        };

        /// <summary>Whether the scan still knows about a room by this id.</summary>
        public static bool HasRoom(IReadOnlyList<RoomScan> rooms, string roomId)
        {
            if (rooms == null || string.IsNullOrEmpty(roomId)) return false;

            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].roomId == roomId) return true;

            return false;
        }

        /// <summary>The room a world point stands in, or null when it stands in none of them.</summary>
        /// <remarks>
        /// A point exactly on the shared edge of two rooms belongs to the one that starts there, which
        /// is what a floor standing on a doorway does; it is not a gap to be missed.
        ///
        /// When the rooms overlap — a room grown from a player's walk reaching into a bigger box —
        /// the smaller one wins, so a point inside both belongs to the more specific room rather than
        /// whichever arrived first. Rooms that do not overlap are unaffected: exactly one contains the
        /// point, and if none does the answer is null.
        /// </remarks>
        public static string FindRoomIdAt(IReadOnlyList<RoomScan> rooms, Vector3 worldPoint)
        {
            if (rooms == null) return null;

            var probe = new Vector2(worldPoint.x, worldPoint.z);

            string found = null;
            float foundArea = float.MaxValue;

            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (string.IsNullOrEmpty(room.roomId)) continue;
                if (!room.boundsXZ.Contains(probe)) continue;

                float area = room.boundsXZ.width * room.boundsXZ.height;
                if (area >= foundArea) continue;

                found = room.roomId;
                foundArea = area;
            }

            return found;
        }

        /// <summary>
        /// How far outside a room the head may stand and still count as being in it while the walk is
        /// being read for doorways, in metres.
        /// </summary>
        /// <remarks>
        /// The boxes come from where the player named them — a couple of metres around where they
        /// stood — so the walk often spends a step or two in the floor between two of them. Without a
        /// tolerance that step reads as "nowhere", the crossing is lost, and the two rooms end up with
        /// no door between them even though the player walked straight from one to the other.
        ///
        /// It is deliberately small, and it is not on its own enough to make a door. A door is only
        /// recorded when the head walks into a room, and the last room it was inside was a different
        /// one, and the walk did not properly leave every room in between (see
        /// <see cref="BuildLinks"/>). Two rooms that merely sit near each other are not joined by a door
        /// because the head passed between them, however close together they are.
        /// </remarks>
        public const float WalkThroughToleranceMeters = 0.5f;

        /// <summary>
        /// The room a world point belongs to when standing a little way outside a room still counts,
        /// and whether the point was inside a room at all.
        /// </summary>
        /// <remarks>
        /// Inside beats near: a point that stands in a room is in that room even if a neighbour's box
        /// reaches closer to the head, so stepping into a room is seen the moment it happens. Among
        /// rooms the point is only near, the nearest wins and ties go to the smaller box, so the answer
        /// does not depend on the order the rooms were discovered in.
        /// </remarks>
        public static string FindRoomIdNear(IReadOnlyList<RoomScan> rooms, Vector3 worldPoint,
            float toleranceMeters, out bool contained)
        {
            contained = false;
            if (rooms == null || toleranceMeters < 0f) return null;

            var probe = new Vector2(worldPoint.x, worldPoint.z);

            string insideRoom = null;
            float insideArea = float.MaxValue;
            string nearRoom = null;
            float nearDistance = float.MaxValue;
            float nearArea = float.MaxValue;

            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (string.IsNullOrEmpty(room.roomId)) continue;

                float area = room.boundsXZ.width * room.boundsXZ.height;

                if (room.boundsXZ.Contains(probe))
                {
                    if (area >= insideArea) continue;

                    insideRoom = room.roomId;
                    insideArea = area;
                    continue;
                }

                float distance = DistanceTo(room.boundsXZ, probe);
                if (distance > toleranceMeters) continue;

                bool nearer = distance < nearDistance;
                bool tiedAndSmaller = Mathf.Approximately(distance, nearDistance) && area < nearArea;
                if (!nearer && !tiedAndSmaller) continue;

                nearRoom = room.roomId;
                nearDistance = distance;
                nearArea = area;
            }

            if (insideRoom == null) return nearRoom;

            contained = true;
            return insideRoom;
        }

        /// <summary>How far a point stands outside a rectangle, measured in the XZ plane.</summary>
        public static float DistanceTo(Rect boundsXZ, Vector2 point)
        {
            float dx = Mathf.Max(boundsXZ.xMin - point.x, 0f, point.x - boundsXZ.xMax);
            float dy = Mathf.Max(boundsXZ.yMin - point.y, 0f, point.y - boundsXZ.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// How far a room grown from the player's walk may reach from the spot it was named at, in
        /// metres.
        /// </summary>
        /// <remarks>
        /// Growth follows the player while they walk around the room they named, so a room named in a
        /// corner grows to the shape of the room they actually have. It has to stop somewhere: a box
        /// that followed the head all the way to the next room would swallow it, and naming the next
        /// room would then rename the first one. Capping the reach from where the room was named — not
        /// merely capping how big the box may get — is what stops it sliding across the house.
        /// </remarks>
        public const float DefaultMaxRoomHalfExtentMeters = 2.5f;

        /// <summary>
        /// How much floor is kept around the furthest place the player walked, so the walls do not
        /// stand on the path.
        /// </summary>
        public const float DefaultGrowthMarginMeters = 0.4f;

        /// <summary>A small box centred on the spot the player named, for their walk to grow.</summary>
        /// <remarks>
        /// This is how a house gets more than one room when the scan found nothing: the player stands
        /// somewhere and says "this is the kitchen", and that is enough to make a room there. It is
        /// deliberately small — two metres across — because the walk is what shapes it; a bigger seed
        /// would swallow the next room before the player had walked into it.
        /// </remarks>
        public static RoomScan CreateRoomAt(string roomId, Vector3 worldPoint,
            float halfExtentMeters = 1f, float wallHeight = 2.4f)
        {
            float half = Mathf.Max(0.25f, halfExtentMeters);

            return new RoomScan
            {
                roomId = roomId,
                boundsXZ = Rect.MinMaxRect(
                    worldPoint.x - half, worldPoint.z - half,
                    worldPoint.x + half, worldPoint.z + half),
                floorY = 0f,
                wallHeight = wallHeight,
            };
        }

        /// <summary>
        /// An id for a new room that nothing is already using, so a room the player names never lands
        /// on top of one the scan found or one they named earlier.
        /// </summary>
        /// <param name="alsoTaken">
        /// Ids that are in use but not in <paramref name="rooms"/> — the house map knows the ids of
        /// rooms it did not adopt, and a new room must not take one of those either.
        /// </param>
        public static string NextRoomId(IReadOnlyList<RoomScan> rooms, IEnumerable<string> alsoTaken = null)
        {
            for (int i = 1; i < 1000; i++)
            {
                string id = "Room-" + i.ToString("000");
                if (HasRoom(rooms, id)) continue;

                bool taken = false;
                if (alsoTaken != null)
                {
                    foreach (var other in alsoTaken)
                    {
                        if (other != id) continue;
                        taken = true;
                        break;
                    }
                }

                if (!taken) return id;
            }

            // A house with a thousand rooms is not a house; this only exists so the method always
            // answers with something unique rather than a name that is already in use.
            return "Room-" + System.DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// Extends a room to cover where the player just walked, and says whether that changed it.
        /// </summary>
        /// <remarks>
        /// Three things stop it: a sample that lands in another room (that floor belongs to the
        /// neighbour, and growing over it would swallow a room someone has already named), a grown box
        /// that would overlap a neighbour, and the reach from the spot the room was named at. Refusing
        /// is the point of the last two — a room grows to fit where the player walked, never over the
        /// top of somewhere else.
        /// </remarks>
        /// <param name="anchor">Where the player stood when they named this room; growth is capped around it.</param>
        /// <summary>
        /// Stretches rooms that face each other across a gap until they meet, so a house drawn from named spots is
        /// a joined-up plan rather than boxes scattered over the floor. Each side of a gap takes half of it.
        /// Only rooms that overlap along the other axis by at least half a metre are joined, and only across gaps of
        /// <paramref name="maxGapMeters"/> or less; running it again changes nothing.
        /// </summary>
        public static bool CloseGaps(List<RoomScan> rooms, float maxGapMeters = 6f)
        {
            if (rooms == null) return false;

            bool changed = false;
            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    var a = rooms[i].boundsXZ;
                    var b = rooms[j].boundsXZ;
                    float overlapZ = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    float overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    float gapX = Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax);
                    float gapZ = Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax);

                    if (overlapZ >= 0.5f && gapX > 0.01f && gapX <= maxGapMeters)
                    {
                        float half = gapX * 0.5f;
                        bool aLeft = a.xMax < b.xMin;
                        a = aLeft ? Rect.MinMaxRect(a.xMin, a.yMin, a.xMax + half, a.yMax) : Rect.MinMaxRect(a.xMin - half, a.yMin, a.xMax, a.yMax);
                        b = aLeft ? Rect.MinMaxRect(b.xMin - half, b.yMin, b.xMax, b.yMax) : Rect.MinMaxRect(b.xMin, b.yMin, b.xMax + half, b.yMax);
                    }
                    else if (overlapX >= 0.5f && gapZ > 0.01f && gapZ <= maxGapMeters)
                    {
                        float half = gapZ * 0.5f;
                        bool aBelow = a.yMax < b.yMin;
                        a = aBelow ? Rect.MinMaxRect(a.xMin, a.yMin, a.xMax, a.yMax + half) : Rect.MinMaxRect(a.xMin, a.yMin - half, a.xMax, a.yMax);
                        b = aBelow ? Rect.MinMaxRect(b.xMin, b.yMin - half, b.xMax, b.yMax) : Rect.MinMaxRect(b.xMin, b.yMin, b.xMax, b.yMax + half);
                    }
                    else continue;

                    var ra = rooms[i]; ra.boundsXZ = a; rooms[i] = ra;
                    var rb = rooms[j]; rb.boundsXZ = b; rooms[j] = rb;
                    changed = true;
                }
            }

            return changed;
        }

        public static bool TryGrowRoom(List<RoomScan> rooms, string roomId, Vector2 anchor, Vector2 sample,
            float marginMeters = DefaultGrowthMarginMeters,
            float maxHalfExtentMeters = DefaultMaxRoomHalfExtentMeters)
        {
            if (rooms == null || string.IsNullOrEmpty(roomId)) return false;

            int index = -1;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].roomId != roomId) continue;
                index = i;
                break;
            }

            if (index < 0) return false;

            var room = rooms[index];
            float margin = Mathf.Max(0f, marginMeters);

            // The room as this sample would leave it: wherever the player has been, plus a margin so
            // the walls do not stand on their path.
            float minX = Mathf.Min(room.boundsXZ.xMin, sample.x - margin);
            float maxX = Mathf.Max(room.boundsXZ.xMax, sample.x + margin);
            float minZ = Mathf.Min(room.boundsXZ.yMin, sample.y - margin);
            float maxZ = Mathf.Max(room.boundsXZ.yMax, sample.y + margin);

            // Clamped to how far the room may reach from where it was named, so a long walk cannot drag
            // the kitchen across the hall. The clamp only ever holds growth back: the edges the room
            // already has are kept, or a room bigger than the reach would be shrunk by walking in it.
            float reach = Mathf.Max(0.25f, maxHalfExtentMeters);
            minX = Mathf.Min(room.boundsXZ.xMin, Mathf.Max(minX, anchor.x - reach));
            maxX = Mathf.Max(room.boundsXZ.xMax, Mathf.Min(maxX, anchor.x + reach));
            minZ = Mathf.Min(room.boundsXZ.yMin, Mathf.Max(minZ, anchor.y - reach));
            maxZ = Mathf.Max(room.boundsXZ.yMax, Mathf.Min(maxZ, anchor.y + reach));

            // The cap can leave nothing to grow into when the player is already at the edge of it.
            if (maxX - minX < 0.1f || maxZ - minZ < 0.1f) return false;

            var grown = Rect.MinMaxRect(minX, minZ, maxX, maxZ);

            for (int i = 0; i < rooms.Count; i++)
            {
                if (i == index) continue;

                var other = rooms[i];
                if (string.IsNullOrEmpty(other.roomId)) continue;
                if (other.boundsXZ.Contains(sample)) return false;
                if (grown.Overlaps(other.boundsXZ)) return false;
            }

            if (Mathf.Approximately(grown.xMin, room.boundsXZ.xMin) &&
                Mathf.Approximately(grown.xMax, room.boundsXZ.xMax) &&
                Mathf.Approximately(grown.yMin, room.boundsXZ.yMin) &&
                Mathf.Approximately(grown.yMax, room.boundsXZ.yMax))
            {
                return false;
            }

            room.boundsXZ = grown;
            rooms[index] = room;
            return true;
        }

        /// <summary>
        /// Every doorway the walk implies, in the order the player walked them.
        /// </summary>
        /// <remarks>
        /// Walking back and forth through the same opening must not draw a second door, so a crossing
        /// that lands close to one already recorded for the same pair of rooms is that door again and
        /// is dropped; one far enough away is a genuinely different opening and is kept.
        ///
        /// A door is only recorded when the head walks *into* a room, and the room it was last inside
        /// was a different one. Merely passing close to a room does not count, so two rooms that
        /// happen to sit near each other are not joined by a door because the walk went between them.
        ///
        /// The tolerance is what makes the last step across a doorway count. The boxes come from where
        /// the player stood when they named each room, so a walk out of one and into the next often
        /// crosses a step or two of floor that belongs to neither; the tolerance keeps that step on
        /// the map. If the walk instead goes properly out of reach of every room, the thread is
        /// dropped: a long stretch of unassigned floor is not a doorway, and inventing one would draw
        /// links that do not exist.
        /// </remarks>
        /// <param name="sameDoorMeters">
        /// How close two crossings of the same pair must be to be the same door.
        /// </param>
        /// <param name="crossingToleranceMeters">
        /// How far outside a room a sample still counts as being on the map rather than out of it.
        /// </param>
        public static List<RoomLink> BuildLinks(IReadOnlyList<RoomScan> rooms, IReadOnlyList<Vector3> headPath,
            float sameDoorMeters = 0.6f, float crossingToleranceMeters = WalkThroughToleranceMeters)
        {
            var links = new List<RoomLink>();
            if (rooms == null || headPath == null || headPath.Count < 2) return links;

            string lastRoom = null;
            bool leftTheMap = false;

            for (int i = 0; i < headPath.Count; i++)
            {
                string here = FindRoomIdNear(rooms, headPath[i], crossingToleranceMeters, out bool inside);

                if (here == null)
                {
                    // Out of reach of every room: whatever happens next is not the doorway the walk
                    // left through.
                    leftTheMap = true;
                    continue;
                }

                // Near a room but not in it: the walk is still on the map, but nothing has happened.
                if (!inside) continue;

                if (lastRoom != null && lastRoom != here && !leftTheMap)
                {
                    var crossing = (headPath[i - 1] + headPath[i]) * 0.5f;
                    AddDoor(links, lastRoom, here, crossing, sameDoorMeters);
                }

                lastRoom = here;
                leftTheMap = false;
            }

            return links;
        }

        /// <summary>
        /// Whether a list of doorways already records this one: the same pair of rooms, crossed in the
        /// same place.
        /// </summary>
        /// <remarks>
        /// "Same door" is a distance and not just a pair of names, because a house can have two doors
        /// between the same two rooms; and it is the same answer whichever way the door was walked,
        /// because <see cref="RoomLink.PairKey"/> puts the two rooms in a fixed order. That is what
        /// stops a player pacing through a doorway from drawing a second one, and what lets a door
        /// found on this walk sit beside the doors restored from the last one.
        /// </remarks>
        public static bool RecordsDoor(IReadOnlyList<RoomLink> links, RoomLink door,
            float sameDoorMeters = 0.6f)
        {
            if (links == null || !door.IsValid) return false;

            for (int i = 0; i < links.Count; i++)
            {
                if (links[i].PairKey != door.PairKey) continue;
                if (Vector3.Distance(links[i].crossingPoint, door.crossingPoint) <= sameDoorMeters) return true;
            }

            return false;
        }

        static void AddDoor(List<RoomLink> links, string from, string to, Vector3 crossing, float sameDoorMeters)
        {
            var link = new RoomLink { fromRoomId = from, toRoomId = to, crossingPoint = crossing };
            if (RecordsDoor(links, link, sameDoorMeters)) return;

            links.Add(link);
        }

        /// <summary>
        /// Applies a name the player gave to whichever room they were standing in when they said it,
        /// and says whether that found a room to put it on.
        /// </summary>
        /// <remarks>
        /// A name said in a stretch of floor that belongs to no room is dropped rather than kept in a
        /// limbo list: there is nothing to attach it to, and the player can simply say it again once
        /// they have stepped into a room.
        /// </remarks>
        public static bool TryNameRoomAt(IReadOnlyList<RoomScan> rooms, Vector3 worldPoint, string name,
            out RoomName named)
        {
            named = default;

            if (string.IsNullOrEmpty(name)) return false;

            string roomId = FindRoomIdAt(rooms, worldPoint);
            if (string.IsNullOrEmpty(roomId)) return false;

            named = NameFor(roomId, name);
            return true;
        }

        /// <summary>A name for a room, stamped with the moment the player said it.</summary>
        public static RoomName NameFor(string roomId, string name)
        {
            return new RoomName
            {
                roomId = roomId,
                name = name,
                namedAtUtc = DateTime.UtcNow.ToString("o"),
            };
        }

        /// <summary>Room ids that have a name, in the order they were named.</summary>
        public static List<string> NamedRoomIds(IReadOnlyList<RoomName> names)
        {
            var ids = new List<string>();
            if (names == null) return ids;

            for (int i = 0; i < names.Count; i++)
                if (names[i].IsValid && !ids.Contains(names[i].roomId)) ids.Add(names[i].roomId);

            return ids;
        }

        /// <summary>
        /// Applies a name, replacing whatever the room was called before.
        /// </summary>
        /// <remarks>
        /// A player who says "this is the kitchen" and later "call this the scullery" has renamed the
        /// room, not created a second one, so the room keeps its place in the list and its old name is
        /// gone. Keeping both would leave the dollhouse with two labels on one room and no way to tell
        /// which is current.
        /// </remarks>
        public static bool ApplyName(List<RoomName> names, RoomName named)
        {
            if (names == null || !named.IsValid) return false;

            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].roomId != named.roomId) continue;
                names[i] = named;
                return true;
            }

            names.Add(named);
            return true;
        }

        /// <summary>What the room is called, or null if nobody has said. The last name wins.</summary>
        public static string NameOf(IReadOnlyList<RoomName> names, string roomId)
        {
            if (names == null || string.IsNullOrEmpty(roomId)) return null;

            string found = null;
            for (int i = 0; i < names.Count; i++)
                if (names[i].IsValid && names[i].roomId == roomId) found = names[i].name;

            return found;
        }
    }
}

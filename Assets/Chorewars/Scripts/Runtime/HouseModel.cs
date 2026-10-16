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

        /// <summary>The room a world point stands in, or null when it stands in none of them.</summary>        /// <remarks>
        /// A point exactly on the shared edge of two rooms belongs to the one that starts there, which
        /// is what a floor standing on a doorway does; it is not a gap to be missed.
        /// </remarks>
        public static string FindRoomIdAt(IReadOnlyList<RoomScan> rooms, Vector3 worldPoint)
        {
            if (rooms == null) return null;

            var probe = new Vector2(worldPoint.x, worldPoint.z);

            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (string.IsNullOrEmpty(room.roomId)) continue;
                if (!room.boundsXZ.Contains(probe)) continue;

                return room.roomId;
            }

            return null;
        }

        /// <summary>
        /// Every doorway the walk implies, in the order the player walked them.
        /// </summary>
        /// <remarks>
        /// Walking back and forth through the same opening must not draw a second door, so a crossing
        /// that lands close to one already recorded for the same pair of rooms is that door again and
        /// is dropped; one far enough away is a genuinely different opening and is kept. A sample that
        /// stands in no room at all ends the walk for a moment rather than counting as a wall crossed:
        /// an unassigned stretch of floor is not a doorway, and inventing one would draw links that do
        /// not exist.
        /// </remarks>
        /// <param name="sameDoorMeters">
        /// How close two crossings of the same pair must be to be the same door.
        /// </param>
        public static List<RoomLink> BuildLinks(IReadOnlyList<RoomScan> rooms, IReadOnlyList<Vector3> headPath,
            float sameDoorMeters = 0.6f)
        {
            var links = new List<RoomLink>();
            if (rooms == null || headPath == null || headPath.Count < 2) return links;

            // PairKey -> where each door recorded for that pair stands, so "same door" is a distance
            // and not just a pair of names (a house can have two doors between the same two rooms).
            var doorsByPair = new Dictionary<string, List<Vector3>>(8);

            string previousRoom = null;

            for (int i = 0; i < headPath.Count; i++)
            {
                string here = FindRoomIdAt(rooms, headPath[i]);

                if (here == null)
                {
                    previousRoom = null;
                    continue;
                }

                if (previousRoom != null && previousRoom != here)
                {
                    var crossing = (headPath[i - 1] + headPath[i]) * 0.5f;
                    AddDoor(links, doorsByPair, previousRoom, here, crossing, sameDoorMeters);
                }

                previousRoom = here;
            }

            return links;
        }

        static void AddDoor(List<RoomLink> links, Dictionary<string, List<Vector3>> doorsByPair,
            string from, string to, Vector3 crossing, float sameDoorMeters)
        {
            var link = new RoomLink { fromRoomId = from, toRoomId = to, crossingPoint = crossing };
            var key = link.PairKey;

            if (!doorsByPair.TryGetValue(key, out var doors))
            {
                doors = new List<Vector3>(2);
                doorsByPair[key] = doors;
            }

            for (int i = 0; i < doors.Count; i++)
                if (Vector3.Distance(doors[i], crossing) <= sameDoorMeters) return;

            doors.Add(crossing);
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

            named = new RoomName
            {
                roomId = roomId,
                name = name,
                namedAtUtc = DateTime.UtcNow.ToString("o"),
            };

            return true;
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

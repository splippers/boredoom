using System.Collections.Generic;
using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    /// <summary>
    /// The dollhouse map's arithmetic, tested away from a headset: rectangles and a straight line in,
    /// names and doorways out.
    /// </summary>
    public class HouseModelTests
    {
        static RoomScan Room(string id, float minX, float minZ, float maxX, float maxZ)
        {
            return new RoomScan
            {
                roomId = id,
                boundsXZ = Rect.MinMaxRect(minX, minZ, maxX, maxZ),
                floorY = 0f,
                wallHeight = 2.4f,
            };
        }

        static List<RoomScan> TwoRoomsThroughADoorway()
        {
            // 3 m rooms sharing a wall at z = 0, with a doorway gap in the middle of it that this
            // model does not need to know about: it only walks the head path.
            return new List<RoomScan>
            {
                Room("kitchen", -1.5f, 0f, 1.5f, 3f),
                Room("hall", -1.5f, -3f, 1.5f, 0f),
            };
        }

        static List<Vector3> StraightWalk(Vector3 from, Vector3 to, int steps)
        {
            var path = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
                path.Add(Vector3.Lerp(from, to, i / (float)steps));
            return path;
        }

        /// <summary>Walks each leg in turn, as one continuous path (no teleport between legs).</summary>
        static List<Vector3> WalkPolyline(params Vector3[] waypoints)
        {
            var path = new List<Vector3>();
            for (int i = 1; i < waypoints.Length; i++)
            {
                var leg = StraightWalk(waypoints[i - 1], waypoints[i], 20);
                // Drop the duplicate waypoint shared with the previous leg.
                path.AddRange(i == 1 ? leg : leg.GetRange(1, leg.Count - 1));
            }
            return path;
        }

        [Test]
        public void FindRoomIdAt_ReturnsTheRoomContainingThePoint()
        {
            var rooms = TwoRoomsThroughADoorway();

            Assert.AreEqual("kitchen", HouseMapBuilder.FindRoomIdAt(rooms, new Vector3(0.5f, 1.6f, 1.5f)));
            Assert.AreEqual("hall", HouseMapBuilder.FindRoomIdAt(rooms, new Vector3(-0.5f, 1.6f, -1.5f)));
        }

        [Test]
        public void FindRoomIdAt_ReturnsNullOutsideEveryRoom()
        {
            var rooms = TwoRoomsThroughADoorway();

            Assert.IsNull(HouseMapBuilder.FindRoomIdAt(rooms, new Vector3(9f, 1.6f, 9f)));
        }

        [Test]
        public void FindRoomIdAt_IgnoresHeadHeight()
        {
            // The player floats at head height; the room footprint is on the floor. A point at
            // y = 1.63 must still land in the room.
            var rooms = TwoRoomsThroughADoorway();

            Assert.AreEqual("kitchen", HouseMapBuilder.FindRoomIdAt(rooms, new Vector3(0f, 1.63f, 2f)));
        }

        [Test]
        public void BuildLinks_WalkingRoomToRoom_RecordsOneDoorAtTheThreshold()
        {
            var rooms = TwoRoomsThroughADoorway();
            var path = StraightWalk(new Vector3(0f, 1.6f, 2f), new Vector3(0f, 1.6f, -2f), 40);

            var links = HouseMapBuilder.BuildLinks(rooms, path);

            Assert.AreEqual(1, links.Count, "one walk through one doorway is one door");
            Assert.AreEqual("kitchen", links[0].fromRoomId);
            Assert.AreEqual("hall", links[0].toRoomId);
            Assert.IsTrue(links[0].IsValid);

            // The crossing is recorded on the room boundary, not at either end of the walk.
            Assert.Less(Mathf.Abs(links[0].crossingPoint.z), 0.2f);
            Assert.Less(Mathf.Abs(links[0].crossingPoint.x), 0.2f);
        }

        [Test]
        public void BuildLinks_BackAndForthThroughTheSameDoor_IsStillOneDoor()
        {
            var rooms = TwoRoomsThroughADoorway();

            var there = StraightWalk(new Vector3(0f, 1.6f, 1.5f), new Vector3(0f, 1.6f, -1.5f), 20);
            var back = StraightWalk(new Vector3(0f, 1.6f, -1.5f), new Vector3(0f, 1.6f, 1.5f), 20);
            var path = new List<Vector3>(there);
            path.AddRange(back);

            var links = HouseMapBuilder.BuildLinks(rooms, path);

            Assert.AreEqual(1, links.Count, "pacing through one doorway must not draw two doors");
        }

        [Test]
        public void BuildLinks_TwoSeparateOpeningsBetweenTheSameRooms_AreTwoDoors()
        {
            var rooms = TwoRoomsThroughADoorway();

            // A continuous walk: out of the kitchen through one opening, along the hall, and back
            // into the kitchen through a second one 2.4 m away. Two genuinely different openings
            // between the same two rooms, so two doors.
            var path = WalkPolyline(
                new Vector3(-1.2f, 1.6f, 1f),
                new Vector3(-1.2f, 1.6f, -1f),
                new Vector3(1.2f, 1.6f, -1f),
                new Vector3(1.2f, 1.6f, 1f));

            var links = HouseMapBuilder.BuildLinks(rooms, path);

            Assert.AreEqual(2, links.Count, "two openings, two doors, even between the same pair of rooms");
            Assert.AreEqual(links[0].PairKey, links[1].PairKey, "both openings join the same two rooms");
            Assert.AreEqual(2, HouseMapBuilder.BuildLinks(rooms, path, 0.3f).Count,
                "a tighter same-door radius must still keep both openings");
        }

        [Test]
        public void BuildLinks_WalkingThroughSpaceThatIsNotARoom_DrawsNoDoor()
        {
            // The rooms are far enough apart that the walk really does leave the map between them: a
            // long unassigned stretch of floor is not a doorway, and inventing a link would draw a
            // door where the player simply walked. (A step or two of floor between two rooms is a
            // different thing, and is a door — see WalkGrownRoomTests.)
            var rooms = new List<RoomScan>
            {
                Room("kitchen", -1.5f, 0f, 1.5f, 3f),
                Room("hall", -1.5f, -6f, 1.5f, -4f),
            };
            var path = StraightWalk(new Vector3(0f, 1.6f, 2f), new Vector3(0f, 1.6f, -5f), 80);

            var links = HouseMapBuilder.BuildLinks(rooms, path);

            Assert.AreEqual(0, links.Count);
        }

        [Test]
        public void BuildLinks_WhereThePlayerNeverLeftOneRoom_DrawsNothing()
        {
            var rooms = TwoRoomsThroughADoorway();
            var path = StraightWalk(new Vector3(-1f, 1.6f, 0.5f), new Vector3(1f, 1.6f, 2.5f), 20);

            var links = HouseMapBuilder.BuildLinks(rooms, path);

            Assert.AreEqual(0, links.Count);
        }

        [Test]
        public void PairKey_IsTheSameDoorWhicheverWayItWasWalked()
        {
            var there = new RoomLink { fromRoomId = "kitchen", toRoomId = "hall" };
            var back = new RoomLink { fromRoomId = "hall", toRoomId = "kitchen" };

            Assert.AreEqual(there.PairKey, back.PairKey);
        }

        [Test]
        public void PairKey_AReversedRoomIdPair_IsNotTheSameRoom()
        {
            var a = new RoomLink { fromRoomId = "ab", toRoomId = "c" };
            var b = new RoomLink { fromRoomId = "a", toRoomId = "bc" };

            Assert.AreNotEqual(a.PairKey, b.PairKey);
        }

        [Test]
        public void TryNameRoomAt_NamesTheRoomThePlayerIsStandingIn()
        {
            var rooms = TwoRoomsThroughADoorway();

            bool named = HouseMapBuilder.TryNameRoomAt(rooms, new Vector3(0f, 1.63f, 2f), "Kitchen", out var roomName);

            Assert.IsTrue(named);
            Assert.IsTrue(roomName.IsValid);
            Assert.AreEqual("kitchen", roomName.roomId);
            Assert.AreEqual("Kitchen", roomName.name);
            Assert.IsNotEmpty(roomName.namedAtUtc);
        }

        [Test]
        public void TryNameRoomAt_OnFloorThatBelongsToNoRoom_RefusesRatherThanGuessing()
        {
            var rooms = TwoRoomsThroughADoorway();

            bool named = HouseMapBuilder.TryNameRoomAt(rooms, new Vector3(9f, 1.63f, 9f), "Kitchen", out _);

            Assert.IsFalse(named);
        }

        [Test]
        public void NamedRoomIds_ListsEachNamedRoomOnce_InNamingOrder()
        {
            var names = new List<RoomName>
            {
                new RoomName { roomId = "kitchen", name = "Kitchen", namedAtUtc = "2026-01-01T00:00:00Z" },
                new RoomName { roomId = "hall", name = "Hall", namedAtUtc = "2026-01-01T00:01:00Z" },
                new RoomName { roomId = "kitchen", name = "The Kitchen", namedAtUtc = "2026-01-01T00:02:00Z" },
                new RoomName { roomId = "", name = "Nowhere", namedAtUtc = "2026-01-01T00:03:00Z" },
            };

            var ids = HouseMapBuilder.NamedRoomIds(names);

            CollectionAssert.AreEqual(new[] { "kitchen", "hall" }, ids);
        }

        [Test]
        public void FallbackNames_HaveNoDuplicates_AndOfferAnEscapeHatch()
        {
            CollectionAssert.AllItemsAreUnique(HouseMapBuilder.FallbackNames);
            CollectionAssert.Contains(HouseMapBuilder.FallbackNames, "Other");
        }
    }
}

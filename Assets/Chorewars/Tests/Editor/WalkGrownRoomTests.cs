using System.Collections.Generic;
using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    /// <summary>
    /// The rooms the player makes by walking: naming a space that no room covers creates one, their
    /// walk grows it, and walking from one to the next is the door between them.
    /// </summary>
    /// <remarks>
    /// This is the half of the dollhouse that does not need a scan. On a headset where the meshing
    /// yields nothing, the only room would otherwise be the play-space box, which is one room with no
    /// doors and no way to make a second one. These tests are the arithmetic of the alternative:
    /// rectangles and a head path in, rooms, names and doorways out.
    /// </remarks>
    public class WalkGrownRoomTests
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

        static List<Vector3> StraightWalk(Vector3 from, Vector3 to, int samples)
        {
            var path = new List<Vector3>(samples);
            for (int i = 0; i < samples; i++)
            {
                float t = samples <= 1 ? 0f : i / (float)(samples - 1);
                path.Add(Vector3.Lerp(from, to, t));
            }

            return path;
        }

        // ---- finding the room a point belongs to ------------------------------------------------

        [Test]
        public void FindRoomIdAt_WhenOneRoomSitsInsideAnother_PicksTheSmaller()
        {
            // A room grown from a walk can end up overlapping a box the scan found. The point belongs
            // to the more specific room, not to whichever happened to be listed first.
            var rooms = new List<RoomScan>
            {
                Room("big", -3f, -3f, 3f, 3f),
                Room("small", -1f, -1f, 1f, 1f),
            };

            Assert.AreEqual("small", HouseMapBuilder.FindRoomIdAt(rooms, new Vector3(0f, 1.6f, 0f)));
            Assert.AreEqual("big", HouseMapBuilder.FindRoomIdAt(rooms, new Vector3(2.5f, 1.6f, 2.5f)));
        }

        [Test]
        public void FindRoomIdNear_ReportsWhetherThePointWasInsideARoom()
        {
            var rooms = new List<RoomScan> { Room("kitchen", -1f, -1f, 1f, 1f) };

            HouseMapBuilder.FindRoomIdNear(rooms, new Vector3(0f, 1.6f, 0f), 0.5f, out bool inside);
            Assert.IsTrue(inside, "a point in the room is inside it");

            HouseMapBuilder.FindRoomIdNear(rooms, new Vector3(0f, 1.6f, 1.3f), 0.5f, out bool nearOnly);
            Assert.IsFalse(nearOnly, "a point just outside the room is near it, not in it");
        }

        [Test]
        public void FindRoomIdNear_PicksTheNearestRoomWithinTheTolerance()
        {
            var rooms = new List<RoomScan>
            {
                Room("kitchen", 0f, 0f, 2f, 2f),
                Room("hall", 3f, 0f, 5f, 2f),
            };

            Assert.AreEqual("kitchen", HouseMapBuilder.FindRoomIdNear(rooms, new Vector3(2.2f, 1.6f, 1f), 0.5f, out _));
            Assert.AreEqual("hall", HouseMapBuilder.FindRoomIdNear(rooms, new Vector3(2.8f, 1.6f, 1f), 0.5f, out _));
        }

        [Test]
        public void FindRoomIdNear_WithAnEquallyNearPair_PicksTheSmallerRoom()
        {
            var rooms = new List<RoomScan>
            {
                Room("kitchen", -2f, 0f, 2f, 2f),
                Room("hall", 3f, 0f, 4f, 1f),
            };

            // Half a metre from the kitchen's edge and half a metre from the hall's: a tie, broken by
            // the smaller box so the answer never depends on the order the rooms arrived in.
            Assert.AreEqual("hall", HouseMapBuilder.FindRoomIdNear(rooms, new Vector3(2.5f, 1.6f, 1f), 0.5f, out _));
        }

        [Test]
        public void FindRoomIdNear_WhenEveryRoomIsTooFar_SaysNowhere()
        {
            var rooms = new List<RoomScan> { Room("kitchen", 0f, 0f, 2f, 2f) };

            Assert.IsNull(HouseMapBuilder.FindRoomIdNear(rooms, new Vector3(9f, 1.6f, 9f), 0.5f, out _));
        }

        // ---- making a room, and growing it ------------------------------------------------------

        [Test]
        public void CreateRoomAt_BoxesTwoMetresAroundTheSpotThePlayerNamed()
        {
            var room = HouseMapBuilder.CreateRoomAt("Room-001", new Vector3(3f, 1.6f, -2f));

            Assert.AreEqual("Room-001", room.roomId);
            Assert.AreEqual(0f, room.floorY, "the tracked floor is the floor");
            Assert.AreEqual(2f, room.boundsXZ.width, 0.001f);
            Assert.AreEqual(2f, room.boundsXZ.height, 0.001f);
            Assert.AreEqual(new Vector2(3f, -2f), room.boundsXZ.center);
            Assert.IsFalse(room.assumed, "a room the player named is a real room, not a stand-in");
        }

        [Test]
        public void NextRoomId_SkipsIdsThatAreAlreadyInUse()
        {
            var rooms = new List<RoomScan> { Room("Room-001", 0f, 0f, 1f, 1f), Room("Room-002", 0f, 0f, 1f, 1f) };

            Assert.AreEqual("Room-003", HouseMapBuilder.NextRoomId(rooms));
        }

        [Test]
        public void NextRoomId_SkipsIdsTheHouseMapKnowsButDidNotAdopt()
        {
            // The play-space stand-in is not a room the map holds, but a new room must not take its id.
            Assert.AreEqual("Room-002", HouseMapBuilder.NextRoomId(new List<RoomScan>(), new[] { "Room-001" }));
        }

        [Test]
        public void TryGrowRoom_GrowsToCoverTheWalkWithAMargin()
        {
            var rooms = new List<RoomScan> { HouseMapBuilder.CreateRoomAt("Room-001", Vector3.zero) };

            bool grew = HouseMapBuilder.TryGrowRoom(rooms, "Room-001", Vector2.zero, new Vector2(1.5f, 0f));

            Assert.IsTrue(grew);
            Assert.AreEqual(1.9f, rooms[0].boundsXZ.xMax, 0.001f, "the wall stands a margin past the furthest step");
            Assert.AreEqual(-1f, rooms[0].boundsXZ.xMin, 0.001f);
        }

        [Test]
        public void TryGrowRoom_StopsAtTheReachCappedAroundTheSpotItWasNamed()
        {
            var rooms = new List<RoomScan> { HouseMapBuilder.CreateRoomAt("Room-001", Vector3.zero) };

            Assert.IsTrue(HouseMapBuilder.TryGrowRoom(rooms, "Room-001", Vector2.zero, new Vector2(5f, 0f)));
            Assert.AreEqual(2.5f, rooms[0].boundsXZ.xMax, 0.001f, "the reach is capped around where the room was named");

            // Already at the cap: walking further must not change it.
            Assert.IsFalse(HouseMapBuilder.TryGrowRoom(rooms, "Room-001", Vector2.zero, new Vector2(7f, 0f)));
        }

        [Test]
        public void TryGrowRoom_RefusesWhenTheSampleLandsInANeighbouringRoom()
        {
            var rooms = new List<RoomScan>
            {
                HouseMapBuilder.CreateRoomAt("Room-001", Vector3.zero),
                Room("Room-002", 2f, 2f, 4f, 4f),
            };

            bool grew = HouseMapBuilder.TryGrowRoom(rooms, "Room-001", Vector2.zero, new Vector2(3f, 3f));

            Assert.IsFalse(grew, "that floor belongs to the neighbour");
            Assert.AreEqual(1f, rooms[0].boundsXZ.xMax, 0.001f, "the room did not move");
        }

        [Test]
        public void TryGrowRoom_RefusesToOverlapANeighbour()
        {
            var rooms = new List<RoomScan>
            {
                HouseMapBuilder.CreateRoomAt("Room-001", Vector3.zero),
                Room("Room-002", 2.3f, -1f, 4.3f, 1f),
            };

            bool grew = HouseMapBuilder.TryGrowRoom(rooms, "Room-001", Vector2.zero, new Vector2(2f, 0f));

            Assert.IsFalse(grew, "growing a margin past that step would swallow the neighbouring room");
            Assert.AreEqual(1f, rooms[0].boundsXZ.xMax, 0.001f);
        }

        [Test]
        public void TryGrowRoom_NeverPullsInARoomLargerThanTheReach()
        {
            // A room restored from disk is anchored at its own centre, so a room wider than the reach is
            // possible. Walking about inside it must not shrink it back to the cap.
            var rooms = new List<RoomScan> { Room("Room-001", -4f, -4f, 4f, 4f) };

            bool grew = HouseMapBuilder.TryGrowRoom(rooms, "Room-001", Vector2.zero, new Vector2(0f, 0f));

            Assert.IsFalse(grew, "nothing to grow into");
            Assert.AreEqual(-4f, rooms[0].boundsXZ.xMin, 0.001f);
            Assert.AreEqual(4f, rooms[0].boundsXZ.xMax, 0.001f);
        }

        [Test]
        public void TryGrowRoom_KeepsTheRoomsOtherFacts()
        {
            var room = HouseMapBuilder.CreateRoomAt("Room-001", Vector3.zero, 1f, 2.7f);
            room.floorY = 0.12f;
            var rooms = new List<RoomScan> { room };

            HouseMapBuilder.TryGrowRoom(rooms, "Room-001", Vector2.zero, new Vector2(1.4f, 0f));

            Assert.AreEqual(2.7f, rooms[0].wallHeight, 0.001f);
            Assert.AreEqual(0.12f, rooms[0].floorY, 0.001f);
        }

        // ---- doors between rooms that only exist because the player named them ------------------

        [Test]
        public void BuildLinks_AShortStretchOfFloorBetweenTwoBoxes_IsStillADoor()
        {
            // The boxes come from where the player stood when they named each room, so there is often
            // a step of floor between them. That step is the doorway, not a reason to lose it.
            var rooms = new List<RoomScan>
            {
                Room("Room-001", -1f, -1f, 1f, 1f),
                Room("Room-002", -1f, 1.6f, 1f, 3.6f),
            };
            var path = StraightWalk(new Vector3(0f, 1.6f, 0f), new Vector3(0f, 1.6f, 3f), 40);

            var links = HouseMapBuilder.BuildLinks(rooms, path);

            Assert.AreEqual(1, links.Count);
            Assert.AreEqual("Room-001", links[0].fromRoomId);
            Assert.AreEqual("Room-002", links[0].toRoomId);
            Assert.That(links[0].crossingPoint.z, Is.InRange(1.0f, 1.8f), "the crossing is in the step between them");
        }

        [Test]
        public void BuildLinks_WalkingPastARoomWithoutGoingIn_DrawsNoDoor()
        {
            // Two rooms that merely sit near each other. The head passes within half a metre of both
            // and never goes into either one, so nothing has been walked between: no door.
            var rooms = new List<RoomScan>
            {
                Room("Room-001", -2f, 0f, 0f, 2f),
                Room("Room-002", 2f, 0f, 4f, 2f),
            };
            var path = StraightWalk(new Vector3(-3f, 1.6f, -0.3f), new Vector3(5f, 1.6f, -0.3f), 80);

            var links = HouseMapBuilder.BuildLinks(rooms, path);

            Assert.AreEqual(0, links.Count);
        }

        [Test]
        public void BuildLinks_TheFirstRoomNamedAtEachEndOfAWalk_EndUpWithADoorBetweenThem()
        {
            // The whole story, in arithmetic: a player names the kitchen, walks about it (the room
            // grows), walks into the next space and names that too. The walk between the two boxes is
            // the door.
            var rooms = new List<RoomScan> { HouseMapBuilder.CreateRoomAt("Room-001", Vector3.zero) };

            HouseMapBuilder.TryGrowRoom(rooms, "Room-001", Vector2.zero, new Vector2(1.4f, 0f));
            HouseMapBuilder.TryGrowRoom(rooms, "Room-001", Vector2.zero, new Vector2(0f, 1.8f));

            rooms.Add(HouseMapBuilder.CreateRoomAt("Room-002", new Vector3(0f, 0f, 4f)));

            var path = StraightWalk(new Vector3(0f, 1.6f, 1.2f), new Vector3(0f, 1.6f, 3.6f), 30);

            var links = HouseMapBuilder.BuildLinks(rooms, path);

            Assert.AreEqual(1, links.Count);
            Assert.AreEqual("Room-001", links[0].fromRoomId);
            Assert.AreEqual("Room-002", links[0].toRoomId);
            Assert.That(links[0].crossingPoint.z, Is.InRange(2.2f, 3.1f), "the crossing is in the step between them");
        }
    }
}

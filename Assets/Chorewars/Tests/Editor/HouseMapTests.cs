using System.Collections.Generic;
using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    /// <summary>Placing a doorway on a wall, and moving the names about.</summary>
    public class HouseDoorwayTests
    {
        static readonly Rect Room = Rect.MinMaxRect(-2f, -1f, 2f, 1f); // 4 m x 2 m

        [Test]
        public void TryFind_OnTheMiddleOfALongWall_OpensWhereThePlayerWentThrough()
        {
            // Crossing the z = -1 wall (wall 2) at x = 0.5.
            bool found = HouseDoorway.TryFind(Room, new Vector2(0.5f, -1f), out var gap);

            Assert.IsTrue(found);
            Assert.IsTrue(gap.alongX, "the z = -1 wall runs along X");
            Assert.AreEqual(-1f, gap.offset, 1e-4f);
            Assert.AreEqual(0.5f, gap.GapCentre, 1e-4f);
            Assert.AreEqual(HouseDoorway.DefaultDoorWidth, gap.GapWidth, 1e-4f);
        }

        [Test]
        public void TryFind_OnAShortWall_ReportsThatWall()
        {
            // Crossing the x = 2 wall at z = 0.2.
            Assert.IsTrue(HouseDoorway.TryFind(Room, new Vector2(2f, 0.2f), out var gap));

            Assert.IsFalse(gap.alongX, "the x = 2 wall runs along Z");
            Assert.AreEqual(2f, gap.offset, 1e-4f);
            Assert.AreEqual(0.2f, gap.GapCentre, 1e-4f);
        }

        [Test]
        public void TryFind_NearACorner_PullsTheOpeningBackInsideTheWall()
        {
            // A 0.9 m door centred 0.1 m from the end of the wall would hang off it.
            Assert.IsTrue(HouseDoorway.TryFind(Room, new Vector2(-1.9f, -1f), out var gap));

            Assert.GreaterOrEqual(gap.gapMin, Room.xMin - 1e-4f);
            Assert.LessOrEqual(gap.gapMax, Room.xMax + 1e-4f);
            Assert.AreEqual(HouseDoorway.DefaultDoorWidth, gap.GapWidth, 1e-4f,
                "the door keeps its width, it just moves along");
        }

        [Test]
        public void TryFind_OnAWallShorterThanTheDoorway_OpensTheWholeWall()
        {
            // A corridor 0.6 m wide: the wall the player crosses is shorter than a door.
            var narrow = Rect.MinMaxRect(0f, 0f, 4f, 0.6f);

            Assert.IsTrue(HouseDoorway.TryFind(narrow, new Vector2(0f, 0.3f), out var gap));

            Assert.IsFalse(gap.alongX, "the x = 0 wall runs along Z");
            Assert.AreEqual(0f, gap.gapMin, 1e-4f);
            Assert.AreEqual(0.6f, gap.gapMax, 1e-4f);
        }

        [Test]
        public void TryFind_InTheMiddleOfTheRoom_OpensNothing()
        {
            // There is no wall in the middle of a room, so the crossing is not a doorway.
            var room = Rect.MinMaxRect(-2f, -2f, 2f, 2f);

            Assert.IsFalse(HouseDoorway.TryFind(room, new Vector2(0f, 0f), out _));
        }

        [Test]
        public void TryFind_WithADegenerateRoom_OpensNothing()
        {
            Assert.IsFalse(HouseDoorway.TryFind(new Rect(0f, 0f, 0f, 0f), new Vector2(0f, 0f), out _));
        }

        [Test]
        public void BothRoomsOfALink_PlaceTheirOpeningFromTheSameCrossing()
        {
            // The kitchen above and the hall below share the z = 0 line; the same crossing must put the
            // two openings in the same place or the dollhouse would show two doors.
            var kitchen = Rect.MinMaxRect(-2f, 0f, 2f, 3f);
            var hall = Rect.MinMaxRect(-2f, -3f, 2f, 0f);
            var crossing = new Vector2(1.1f, 0f);

            Assert.IsTrue(HouseDoorway.TryFind(kitchen, crossing, out var fromKitchen));
            Assert.IsTrue(HouseDoorway.TryFind(hall, crossing, out var fromHall));

            Assert.AreEqual(fromKitchen.offset, fromHall.offset, 1e-4f);
            Assert.AreEqual(fromKitchen.GapCentre, fromHall.GapCentre, 1e-4f);
            Assert.AreEqual(fromKitchen.GapWidth, fromHall.GapWidth, 1e-4f);
        }
    }

    public class HouseMapDataTests
    {
        static RoomScan Room(string id) => new RoomScan
        {
            roomId = id,
            boundsXZ = Rect.MinMaxRect(0f, 0f, 1f, 1f),
            floorY = 0f,
            wallHeight = 2.4f,
        };

        [Test]
        public void ApplyName_NamesARoom()
        {
            var names = new List<RoomName>();

            Assert.IsTrue(HouseMapBuilder.ApplyName(names, new RoomName { roomId = "kitchen", name = "Kitchen" }));
            Assert.AreEqual("Kitchen", HouseMapBuilder.NameOf(names, "kitchen"));
        }

        [Test]
        public void ApplyName_SayingItAgainRenamesRatherThanAdds()
        {
            var names = new List<RoomName>();
            HouseMapBuilder.ApplyName(names, new RoomName { roomId = "kitchen", name = "Kitchen" });

            HouseMapBuilder.ApplyName(names, new RoomName { roomId = "kitchen", name = "Scullery" });

            Assert.AreEqual(1, names.Count, "a renamed room is one room");
            Assert.AreEqual("Scullery", HouseMapBuilder.NameOf(names, "kitchen"));
        }

        [Test]
        public void ApplyName_RefusesAnEmptyName()
        {
            var names = new List<RoomName>();

            Assert.IsFalse(HouseMapBuilder.ApplyName(names, new RoomName { roomId = "kitchen", name = "" }));
            Assert.AreEqual(0, names.Count);
        }

        [Test]
        public void NameOf_AnUnnamedRoom_IsNull()
        {
            Assert.IsNull(HouseMapBuilder.NameOf(new List<RoomName>(), "hall"));
        }

        [Test]
        public void HasRoom_OnlyKnowsRoomsTheScanKept()
        {
            var rooms = new List<RoomScan> { Room("kitchen") };

            Assert.IsTrue(HouseMapBuilder.HasRoom(rooms, "kitchen"));
            Assert.IsFalse(HouseMapBuilder.HasRoom(rooms, "cellar"));
        }

        [Test]
        public void SetRooms_KeepsNamesForRoomsThatAreStillThere_AndDropsTheRest()
        {
            var data = new HouseMapData { names = new List<RoomName>
            {
                new RoomName { roomId = "kitchen", name = "Kitchen" },
                new RoomName { roomId = "cellar", name = "Cellar" },
            } };

            data.SetRooms(new List<RoomScan> { Room("kitchen") });

            Assert.AreEqual(1, data.names.Count);
            Assert.AreEqual("kitchen", data.names[0].roomId);
        }
    }

    public class HouseMapStoreTests
    {
        static string TempPath() =>
            System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "boredoom_house_" + System.Guid.NewGuid().ToString("N") + ".json");

        [Test]
        public void RoundTripsRoomsNamesAndLinks()
        {
            string path = TempPath();

            try
            {
                var data = new HouseMapData();
                data.SetRooms(new List<RoomScan>
                {
                    new RoomScan
                    {
                        roomId = "kitchen",
                        boundsXZ = Rect.MinMaxRect(-1.5f, 0f, 1.5f, 3f),
                        floorY = 0.02f,
                        wallHeight = 2.4f,
                    },
                });
                data.names.Add(new RoomName { roomId = "kitchen", name = "Kitchen", namedAtUtc = "2026-10-16T09:00:00Z" });
                data.links.Add(new RoomLink
                {
                    fromRoomId = "kitchen",
                    toRoomId = "hall",
                    crossingPoint = new Vector3(0.1f, 1.2f, 0f),
                });

                Assert.IsTrue(HouseMapStore.Save(data, path), "the map should write");

                var loaded = HouseMapStore.Load(path);

                Assert.AreEqual(1, loaded.rooms.Count);
                Assert.AreEqual("kitchen", loaded.rooms[0].roomId);
                Assert.AreEqual(Rect.MinMaxRect(-1.5f, 0f, 1.5f, 3f), loaded.rooms[0].boundsXZ);
                Assert.AreEqual(0.02f, loaded.rooms[0].floorY, 1e-5f);

                Assert.AreEqual(1, loaded.names.Count);
                Assert.AreEqual("Kitchen", loaded.names[0].name);

                Assert.AreEqual(1, loaded.links.Count);
                // PairKey is lexicographic, so the same door reads hall|kitchen either way it is walked.
                Assert.AreEqual("hall|kitchen", loaded.links[0].PairKey);
                Assert.AreEqual(new Vector3(0.1f, 1.2f, 0f), loaded.links[0].crossingPoint);
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test]
        public void Load_WithNoFile_IsAnEmptyHouseRatherThanAFailure()
        {
            var loaded = HouseMapStore.Load(TempPath());

            Assert.IsNotNull(loaded);
            Assert.AreEqual(0, loaded.rooms.Count);
            Assert.AreEqual(0, loaded.names.Count);
            Assert.AreEqual(0, loaded.links.Count);
        }

        [Test]
        public void Load_WithRubbishInTheFile_ReturnsAnEmptyHouse()
        {
            string path = TempPath();

            try
            {
                System.IO.File.WriteAllText(path, "this is not json {");

                var loaded = HouseMapStore.Load(path);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(0, loaded.names.Count);
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }
    }
}

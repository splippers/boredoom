using System.Collections.Generic;
using System.IO;
using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    /// <summary>
    /// The house map as the player uses it: naming a space makes the room, and the house they have
    /// described survives being put away and picked up again.
    /// </summary>
    /// <remarks>
    /// The driver runs without a pipeline or a presenter here on purpose. Naming is the seam the whole
    /// feature turns on — a headset whose scan yields nothing has one stand-in box and no way to make a
    /// second room unless this makes one — and it has to work when there is nothing to draw it on,
    /// because the miniature is a view of the map and not part of it.
    /// </remarks>
    public class HouseMapDriverTests
    {
        private GameObject _root;
        private HouseMapDriver _driver;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("HouseMapDriverTest");
            _driver = _root.AddComponent<HouseMapDriver>();
            _path = Path.Combine(Path.GetTempPath(), $"boredoom_driver_house_{System.Guid.NewGuid():N}.json");
            _driver.StorePath = _path;
            _driver.Configure(null, null, _root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (File.Exists(_path)) File.Delete(_path);
        }

        [Test]
        public void NameRoomAt_OnFloorThatNoRoomCovers_MakesTheRoomAndNamesIt()
        {
            // The scan found nothing usable: the map starts with no rooms at all, which is the headset
            // case. Saying "this is the kitchen" has to make one.
            Assert.AreEqual(0, _driver.Rooms.Count);

            bool named = _driver.NameRoomAt(new Vector3(0f, 1.6f, 0f), "Kitchen");

            Assert.IsTrue(named, "naming a space with no room in it makes the room");
            Assert.AreEqual(1, _driver.Rooms.Count);
            Assert.AreEqual("Room-001", _driver.Rooms[0].roomId);
            Assert.AreEqual(1, _driver.NamedRoomCount);
            Assert.IsFalse(_driver.Rooms[0].assumed, "a room the player named is not a stand-in");
            Assert.AreEqual(new Vector2(0f, 0f), _driver.Rooms[0].boundsXZ.center, "the room is made where they stood");
        }

        [Test]
        public void NameRoomAt_OnFloorAnotherRoomAlreadyCovers_RenamesThatRoom()
        {
            _driver.NameRoomAt(new Vector3(0f, 1.6f, 0f), "Kitchen");

            bool named = _driver.NameRoomAt(new Vector3(0.5f, 1.6f, 0.5f), "Scullery");

            Assert.IsTrue(named);
            Assert.AreEqual(1, _driver.Rooms.Count, "naming inside a room renames it rather than making another");
            Assert.AreEqual(1, _driver.NamedRoomCount);
        }

        [Test]
        public void NameRoomAt_SomewhereElse_MakesASecondRoomWithItsOwnId()
        {
            _driver.NameRoomAt(new Vector3(0f, 1.6f, 0f), "Kitchen");
            _driver.NameRoomAt(new Vector3(0f, 1.6f, 12f), "Hall");

            Assert.AreEqual(2, _driver.Rooms.Count);
            CollectionAssert.AreEqual(
                new[] { "Room-001", "Room-002" },
                new List<string> { _driver.Rooms[0].roomId, _driver.Rooms[1].roomId });
        }

        [Test]
        public void SaveNow_ThenANewDriver_ComesBackWithTheSameRoomsAndNames()
        {
            _driver.NameRoomAt(new Vector3(0f, 1.6f, 0f), "Kitchen");
            _driver.NameRoomAt(new Vector3(0f, 1.6f, 12f), "Hall");
            _driver.SaveNow();

            var reloaded = new GameObject("HouseMapDriverReloaded").AddComponent<HouseMapDriver>();
            reloaded.StorePath = _path;
            reloaded.Configure(null, null, _root.transform);

            try
            {
                Assert.AreEqual(2, reloaded.Rooms.Count, "the rooms the player made are remembered");
                Assert.AreEqual(2, reloaded.NamedRoomCount);

                // The rooms come back at the same place, so growing them again carries on from there.
                Assert.AreEqual(new Vector2(0f, 0f), reloaded.Rooms[0].boundsXZ.center);
                Assert.AreEqual(new Vector2(0f, 12f), reloaded.Rooms[1].boundsXZ.center);

                // And a reload that is written straight back out does not lose what it read.
                reloaded.SaveNow();

                var again = new GameObject("HouseMapDriverAgain").AddComponent<HouseMapDriver>();
                again.StorePath = _path;
                again.Configure(null, null, _root.transform);

                try
                {
                    Assert.AreEqual(2, again.Rooms.Count);
                    Assert.AreEqual(2, again.NamedRoomCount);
                }
                finally
                {
                    Object.DestroyImmediate(again.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(reloaded.gameObject);
            }
        }

        [Test]
        public void NameRoomAt_SteppedPastTheRoomTheyMade_MakesASecondRoomForTheNewSpace()
        {
            // The seed box is small on purpose: it is the walk that makes a room the size of the space
            // it is in. Naming somewhere the player has not walked is naming a different space.
            _driver.NameRoomAt(new Vector3(0f, 1.6f, 0f), "Kitchen");
            _driver.NameRoomAt(new Vector3(3f, 1.6f, 0f), "Hall");

            Assert.AreEqual(2, _driver.Rooms.Count);
        }
    }
}

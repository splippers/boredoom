using Chorewars.Dollhouse;
using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    /// <summary>
    /// The dollhouse dressings, exercised without a headset. The presenter builds real primitives and
    /// a real TextMesh even in edit mode, so this checks what the player would actually see: a label
    /// over the room, an arch on the wall they walked through, and a line between linked rooms.
    /// </summary>
    public class HouseMapPresenterTests
    {
        private GameObject _root;
        private GameObject _dollhouseGo;
        private DollhouseManager _dollhouse;
        private HouseMapPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("HouseMapTest");

            _dollhouseGo = new GameObject("Dollhouse");
            _dollhouseGo.transform.SetParent(_root.transform, false);
            _dollhouse = _dollhouseGo.AddComponent<DollhouseManager>();

            _presenter = _root.AddComponent<HouseMapPresenter>();
            _presenter.Configure(_dollhouse, null, _root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private static RoomScan Room(string id, float minX, float minZ, float maxX, float maxZ) => new RoomScan
        {
            roomId = id,
            boundsXZ = Rect.MinMaxRect(minX, minZ, maxX, maxZ),
            floorY = 0f,
            wallHeight = 2.4f,
        };

        private TextMesh LabelOf(string roomId)
        {
            var child = _root.transform.Find($"Label-{roomId}");
            return child != null ? child.GetComponent<TextMesh>() : null;
        }

        [Test]
        public void SetName_ShowsTheNameOverTheRoom()
        {
            var kitchen = Room("kitchen", -1.5f, 0f, 1.5f, 3f);

            _presenter.SetRoom(kitchen);
            _presenter.SetName("kitchen", "Kitchen");

            Assert.AreEqual(1, _presenter.LabelledRoomCount);

            var label = LabelOf("kitchen");
            Assert.IsNotNull(label, "the room should have a label");
            Assert.IsTrue(label.gameObject.activeSelf, "a named room's label is shown");
            Assert.AreEqual("Kitchen", label.text);
            Assert.Greater(label.transform.position.y, kitchen.floorY, "the label floats above the floor");
        }

        [Test]
        public void SetName_WithAnEmptyName_HidesTheLabel()
        {
            _presenter.SetRoom(Room("kitchen", -1.5f, 0f, 1.5f, 3f));
            _presenter.SetName("kitchen", "Kitchen");

            _presenter.SetName("kitchen", "");

            Assert.IsFalse(LabelOf("kitchen").gameObject.activeSelf, "an unnamed room shows no name");
        }

        [Test]
        public void OneDoorway_DrawsOneArchOfTwoPostsAndALintel_AndOneLinkLine()
        {
            var kitchen = Room("kitchen", -1.5f, 0f, 1.5f, 3f);
            var hall = Room("hall", -1.5f, -3f, 1.5f, 0f);

            _presenter.SetRoom(kitchen);
            _presenter.SetRoom(hall);
            _presenter.SetLinks(new[]
            {
                new RoomLink { fromRoomId = "kitchen", toRoomId = "hall", crossingPoint = new Vector3(0f, 1.2f, 0f) },
            });

            Assert.AreEqual(3, _presenter.DoorwayCount, "one doorway is two posts and a lintel");
            Assert.AreEqual(1, _presenter.LinkCount, "two linked rooms are joined by one line");
        }

        [Test]
        public void TwoDoorways_DrawTwoArches()
        {
            var kitchen = Room("kitchen", -1.5f, 0f, 1.5f, 3f);
            var hall = Room("hall", -1.5f, -3f, 1.5f, 0f);

            _presenter.SetRoom(kitchen);
            _presenter.SetRoom(hall);
            _presenter.SetLinks(new[]
            {
                new RoomLink { fromRoomId = "kitchen", toRoomId = "hall", crossingPoint = new Vector3(-1f, 1.2f, 0f) },
                new RoomLink { fromRoomId = "kitchen", toRoomId = "hall", crossingPoint = new Vector3(1f, 1.2f, 0f) },
            });

            Assert.AreEqual(6, _presenter.DoorwayCount, "two doorways, two arches");
            Assert.AreEqual(2, _presenter.LinkCount);
        }

        [Test]
        public void SetLinks_Again_ReplacesRatherThanAccumulates()
        {
            var kitchen = Room("kitchen", -1.5f, 0f, 1.5f, 3f);
            var hall = Room("hall", -1.5f, -3f, 1.5f, 0f);

            _presenter.SetRoom(kitchen);
            _presenter.SetRoom(hall);

            var one = new[]
            {
                new RoomLink { fromRoomId = "kitchen", toRoomId = "hall", crossingPoint = new Vector3(0f, 1.2f, 0f) },
            };

            _presenter.SetLinks(one);
            _presenter.SetLinks(one);

            Assert.AreEqual(3, _presenter.DoorwayCount, "the second redraw must not leave the first arch behind");
            Assert.AreEqual(1, _presenter.LinkCount);
        }

        [Test]
        public void ACrossingInTheMiddleOfTheRoom_DrawsALineButNoArch()
        {
            // Defensive: a hand-made link whose crossing is not on a wall has no wall to open, so it
            // draws the connection but no doorway rather than an arch floating in the middle of a room.
            var kitchen = Room("kitchen", -2f, -2f, 2f, 2f);
            var hall = Room("hall", -2f, -3f, 2f, -2.5f);

            _presenter.SetRoom(kitchen);
            _presenter.SetRoom(hall);
            _presenter.SetLinks(new[]
            {
                new RoomLink { fromRoomId = "kitchen", toRoomId = "hall", crossingPoint = new Vector3(0f, 1.2f, 0f) },
            });

            Assert.AreEqual(0, _presenter.DoorwayCount);
            Assert.AreEqual(1, _presenter.LinkCount);
        }
    }
}

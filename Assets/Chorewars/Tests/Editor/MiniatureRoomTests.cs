using Chorewars.Dollhouse;
using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    /// <summary>
    /// The miniature's side of the rooms the player makes by naming one: the house map asks for a box,
    /// the box is the size the map says, and the stand-in can be taken away.
    /// </summary>
    /// <remarks>
    /// These are the boxes a headset with no usable meshing would show. They are built away from the
    /// scan — no source, no coverage map — because that is exactly the state the device is in when the
    /// player names their first room.
    /// </remarks>
    public class MiniatureRoomTests
    {
        private GameObject _root;
        private GameObject _dollhouseGo;
        private DollhouseManager _dollhouse;
        private ScanToDollhousePipeline _pipeline;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("MiniatureRoomTest");

            _dollhouseGo = new GameObject("Dollhouse");
            _dollhouseGo.transform.SetParent(_root.transform, false);
            _dollhouse = _dollhouseGo.AddComponent<DollhouseManager>();

            var pipelineGo = new GameObject("Pipeline");
            pipelineGo.transform.SetParent(_root.transform, false);
            _pipeline = pipelineGo.AddComponent<ScanToDollhousePipeline>();
            _pipeline.Configure(_dollhouse, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private Transform BoxOf(string roomId)
        {
            var miniature = _dollhouseGo.transform.Find("DollhouseMiniature");
            return miniature != null ? miniature.Find($"Mini-{roomId}") : null;
        }

        [Test]
        public void EnsureRoom_BuildsABoxForARoomTheScanNeverFound()
        {
            var kitchen = HouseMapBuilder.CreateRoomAt("Room-001", Vector3.zero);

            bool built = _pipeline.EnsureRoom(kitchen);

            Assert.IsTrue(built, "the map asked for a room the scan never found");
            Assert.AreEqual(1, _pipeline.RoomCount);
            Assert.AreEqual(1, _pipeline.MiniatureRoomCount);

            var box = BoxOf("Room-001");
            Assert.IsNotNull(box, "the room should have a box in the miniature");
            Assert.IsNotNull(box.Find("Floor"), "a room has a floor");
            Assert.AreEqual(5, box.childCount, "a floor and four walls");
        }

        [Test]
        public void EnsureRoom_WithARoomThatHasNotChanged_DoesNotBuildAgain()
        {
            var kitchen = HouseMapBuilder.CreateRoomAt("Room-001", Vector3.zero);

            Assert.IsTrue(_pipeline.EnsureRoom(kitchen));
            Assert.IsFalse(_pipeline.EnsureRoom(kitchen), "nothing has changed, so nothing is rebuilt");
            Assert.AreEqual(1, _pipeline.MiniatureRoomCount);
        }

        [Test]
        public void EnsureRoom_WhenTheWalkHasGrownTheRoom_ShowsItAtItsNewSize()
        {
            var kitchen = HouseMapBuilder.CreateRoomAt("Room-001", Vector3.zero);
            _pipeline.EnsureRoom(kitchen);

            float before = BoxOf("Room-001").Find("Floor").localScale.x;

            var grown = kitchen;
            grown.boundsXZ = Rect.MinMaxRect(-1f, -1f, 3f, 1f);

            Assert.IsTrue(_pipeline.EnsureRoom(grown), "the room's shape changed, so the box is rebuilt");
            Assert.AreEqual(1, _pipeline.RoomCount, "the room is not added a second time");
            Assert.AreEqual(1, _pipeline.MiniatureRoomCount, "and its box is replaced, not duplicated");

            var box = BoxOf("Room-001");
            Assert.IsNotNull(box);
            Assert.Greater(box.Find("Floor").localScale.x, before, "the box is as wide as the room now is");
        }

        [Test]
        public void SetMiniatureVisible_TakesAStandInBoxOutWithoutForgettingTheRoom()
        {
            _pipeline.EnsureRoom(HouseMapBuilder.CreateRoomAt("Room-001", Vector3.zero));

            _pipeline.SetMiniatureVisible("Room-001", false);

            Assert.IsFalse(BoxOf("Room-001").gameObject.activeSelf, "the stand-in is no longer drawn");
            Assert.AreEqual(1, _pipeline.RoomCount, "but it is still part of the map, so cleaning is untouched");
        }

        [Test]
        public void SetMiniatureVisible_ForARoomWithNoBox_IsQuietlyIgnored()
        {
            Assert.DoesNotThrow(() => _pipeline.SetMiniatureVisible("Room-404", false));
            Assert.DoesNotThrow(() => _pipeline.SetMiniatureVisible(null, false));
        }
    }
}

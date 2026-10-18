using System.Collections.Generic;
using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    public class SceneRoomBuilderTests
    {
        private static SceneSurface Floor(float x0, float z0, float x1, float z1, float y = 0f) => new SceneSurface
        {
            kind = SceneSurfaceKind.Floor,
            center = new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f),
            outlineXZ = new[] { new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1) }
        };

        private static SceneSurface Door(float x, float z) => new SceneSurface { kind = SceneSurfaceKind.Door, center = new Vector3(x, 1f, z) };

        [Test]
        public void NothingSeenGivesNoRooms()
        {
            Assert.AreEqual(0, SceneRoomBuilder.Build(new List<SceneSurface>()).rooms.Count);
            Assert.AreEqual(0, SceneRoomBuilder.Build(null).rooms.Count);
        }

        [Test]
        public void EachFloorIsARoom()
        {
            var scene = SceneRoomBuilder.Build(new[] { Floor(0, 0, 4, 3), Floor(5, 0, 8, 3) });

            Assert.AreEqual(2, scene.rooms.Count);
            Assert.AreEqual("Scene-1", scene.rooms[0].roomId, "the larger room comes first");
            Assert.AreEqual(12f, scene.rooms[0].boundsXZ.width * scene.rooms[0].boundsXZ.height, 0.01f);
        }

        [Test]
        public void TwoFloorsOnTopOfEachOtherAreOneRoom()
        {
            var scene = SceneRoomBuilder.Build(new[] { Floor(0, 0, 4, 3), Floor(0.1f, 0.1f, 4, 3) });
            Assert.AreEqual(1, scene.rooms.Count);
        }

        [Test]
        public void ADoorFrameBetweenTwoRoomsLinksThem()
        {
            var scene = SceneRoomBuilder.Build(new[] { Floor(0, 0, 4, 3), Floor(4.4f, 0, 8, 3), Door(4.2f, 1.5f) });

            Assert.AreEqual(1, scene.doors.Count);
            Assert.AreNotEqual(scene.doors[0].fromRoomId, scene.doors[0].toRoomId);
            Assert.IsTrue(scene.doors[0].IsValid);
        }

        [Test]
        public void ADoorToTheOutsideLinksNothing()
        {
            var scene = SceneRoomBuilder.Build(new[] { Floor(0, 0, 4, 3), Door(-0.2f, 1.5f) });
            Assert.AreEqual(0, scene.doors.Count);
        }

        [Test]
        public void TheFloorHeightIsTheLowestFloorAndTheCeilingGivesTheWallHeight()
        {
            var ceiling = new SceneSurface { kind = SceneSurfaceKind.Ceiling, center = new Vector3(2, 2.7f, 1.5f) };
            var scene = SceneRoomBuilder.Build(new[] { Floor(0, 0, 4, 3, -0.12f), ceiling });

            Assert.AreEqual(-0.12f, scene.floorY, 0.001f);
            Assert.AreEqual(2.82f, scene.rooms[0].wallHeight, 0.01f);
        }

        [Test]
        public void ASliverOfFloorIsNotARoom()
        {
            Assert.AreEqual(0, SceneRoomBuilder.Build(new[] { Floor(0, 0, 4, 0.2f) }).rooms.Count);
        }
    }
}

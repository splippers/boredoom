using System.Collections.Generic;
using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    public class CloseGapsTests
    {
        private static RoomScan Room(string id, float x0, float z0, float x1, float z1) =>
            new RoomScan { roomId = id, boundsXZ = Rect.MinMaxRect(x0, z0, x1, z1), wallHeight = 2.4f };

        [Test]
        public void TwoRoomsSideBySideAreStretchedToMeet()
        {
            var rooms = new List<RoomScan> { Room("a", 0, 0, 2, 2), Room("b", 5, 0.5f, 7, 2.5f) };

            Assert.IsTrue(HouseMapBuilder.CloseGaps(rooms));
            Assert.AreEqual(rooms[0].boundsXZ.xMax, rooms[1].boundsXZ.xMin, 0.001f);
            Assert.AreEqual(3.5f, rooms[0].boundsXZ.xMax, 0.001f, "each side takes half the 3 m gap");
        }

        [Test]
        public void RunningItAgainChangesNothing()
        {
            var rooms = new List<RoomScan> { Room("a", 0, 0, 2, 2), Room("b", 5, 0, 7, 2) };
            HouseMapBuilder.CloseGaps(rooms);
            Assert.IsFalse(HouseMapBuilder.CloseGaps(rooms));
        }

        [Test]
        public void RoomsFarApartOrOnlyTouchingAtACornerAreLeftAlone()
        {
            var far = new List<RoomScan> { Room("a", 0, 0, 2, 2), Room("b", 20, 0, 22, 2) };
            var diagonal = new List<RoomScan> { Room("a", 0, 0, 2, 2), Room("b", 4, 5, 6, 7) };

            Assert.IsFalse(HouseMapBuilder.CloseGaps(far));
            Assert.IsFalse(HouseMapBuilder.CloseGaps(diagonal));
        }
    }
}

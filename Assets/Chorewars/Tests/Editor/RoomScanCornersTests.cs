using NUnit.Framework;
using UnityEngine;
using Chorewars.Runtime;

namespace Chorewars.Tests
{
    public class RoomScanCornersTests
    {
        static readonly Vector2[] LShape =
        {
            new Vector2(0, 0), new Vector2(4, 0), new Vector2(4, 2), new Vector2(2, 2), new Vector2(2, 5), new Vector2(0, 5)
        };

        [Test]
        public void ARoomWithNoMeasuredCornersHasNoPolygon()
        {
            var room = new RoomScan { boundsXZ = Rect.MinMaxRect(0, 0, 4, 5) };
            Assert.IsNull(room.FittedCorners());
        }

        [Test]
        public void MeasuredCornersThatFillTheBoxAreUsed()
        {
            var room = new RoomScan { boundsXZ = Rect.MinMaxRect(0, 0, 4, 5), cornersXZ = LShape };
            Assert.AreEqual(6, room.FittedCorners().Length);
        }

        [Test]
        public void CornersAreDroppedOnceTheRoomHasGrownPastThem()
        {
            var room = new RoomScan { boundsXZ = Rect.MinMaxRect(0, 0, 4.8f, 5), cornersXZ = LShape };
            Assert.IsNull(room.FittedCorners());
        }

        [Test]
        public void TwoCornersAreNotAPolygon()
        {
            var room = new RoomScan { boundsXZ = Rect.MinMaxRect(0, 0, 4, 5), cornersXZ = new[] { LShape[0], LShape[1] } };
            Assert.IsNull(room.FittedCorners());
        }
    }
}

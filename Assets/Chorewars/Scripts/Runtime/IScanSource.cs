using System;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>One scanned room: an axis-aligned XZ footprint plus floor height and wall height.</summary>
    [Serializable]
    public struct RoomScan
    {
        public string roomId;
        public Rect boundsXZ;
        public float floorY;
        public float wallHeight;

        /// <summary>
        /// True when the box is a stand-in rather than a measurement of a real room: the play-space
        /// boundary, or a size assumed because the scan could see nothing.
        /// </summary>
        /// <remarks>
        /// The dollhouse may still draw it, so there is something to look at before any room has been
        /// named, but nothing in the house map treats it as a room. Naming a spot inside it makes a
        /// new room rather than relabelling the whole play space as a kitchen, and it is hidden from
        /// the miniature once the player has named a real one. It defaults to false, so a room from a
        /// working scan needs no change.
        /// </remarks>
        public bool assumed;

        /// <summary>
        /// The real corners of the room (x, z), in order round the edge, when they were measured one by one
        /// from the ceiling. Empty for a room known only as a box. It is only believed while it still fits
        /// <see cref="boundsXZ"/>: a room that has since grown or moved is drawn as its box again.
        /// </summary>
        public Vector2[] cornersXZ;

        /// <summary>The measured corners, or null when there are none that still match the box.</summary>
        public Vector2[] FittedCorners()
        {
            if (cornersXZ == null || cornersXZ.Length < 3) return null;

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var c in cornersXZ)
            {
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
            }

            const float tolerance = 0.05f;
            bool fits = Mathf.Abs(minX - boundsXZ.xMin) < tolerance && Mathf.Abs(maxX - boundsXZ.xMax) < tolerance
                     && Mathf.Abs(minY - boundsXZ.yMin) < tolerance && Mathf.Abs(maxY - boundsXZ.yMax) < tolerance;
            return fits ? cornersXZ : null;
        }

        public Bounds ToBounds()
        {
            var size = new Vector3(
                Mathf.Max(0.1f, boundsXZ.width),
                Mathf.Max(0.1f, wallHeight),
                Mathf.Max(0.1f, boundsXZ.height));
            var centre = new Vector3(boundsXZ.center.x, floorY + size.y * 0.5f, boundsXZ.center.y);
            return new Bounds(centre, size);
        }
    }

    /// <summary>
    /// Source of room scan data. Implementations: <c>FakeScanSource</c> (editor, no Quest deps)
    /// and <c>QuestScanSource</c> (device stub around SpatialMeshTracker).
    /// Everything downstream is written against this interface, so the dollhouse never knows
    /// whether the data is real or simulated.
    /// </summary>
    public interface IScanSource
    {
        /// <summary>Raised as each room is discovered.</summary>
        event Action<RoomScan> RoomScanned;

        /// <summary>Raised once when the scan finishes.</summary>
        event Action ScanCompleted;

        /// <summary>Normalised progress, 0..1.</summary>
        float Progress { get; }

        bool IsScanning { get; }

        /// <summary>
        /// Full play-area bounds when known up front (the fake source always knows its plan;
        /// a real scan returns null until it has seen something).
        /// </summary>
        Bounds? KnownBounds { get; }

        void Begin();
        void Stop();
    }
}

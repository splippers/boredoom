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

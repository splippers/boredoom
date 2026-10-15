using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Reads the XR guardian/boundary — the play area the player's own runtime has already measured —
    /// as a last resort for sizing a scan that cannot see the room.
    /// </summary>
    /// <remarks>
    /// This exists because meshing is a provider feature, not a core one: on a plain OpenXR build with
    /// no mesh subsystem, the guardian is the only thing the runtime knows about the space the player
    /// is standing in. It is a much better guess than a made-up box, because the player drew it.
    ///
    /// The rectangle comes back in the space the boundary is reported in, which is tracking space. The
    /// scan runs before the home origin is aligned, so tracking space and world space agree at that
    /// point; anything that needs to hold after alignment should re-derive from the rooms instead.
    /// </remarks>
    public static class PlaySpaceBoundary
    {
        private static readonly List<XRInputSubsystem> Subsystems = new List<XRInputSubsystem>();
        private static readonly List<Vector3> Points = new List<Vector3>();

        /// <summary>
        /// The XZ rectangle around the runtime's boundary, or null when there is none — no boundary
        /// drawn, no XR session, or an API that does not answer. Never throws.
        /// </summary>
        public static Rect? TryGetRect()
        {
            try
            {
                Subsystems.Clear();
                SubsystemManager.GetSubsystems(Subsystems);

                foreach (var subsystem in Subsystems)
                {
                    if (subsystem == null || !subsystem.running) continue;

                    Points.Clear();
                    if (!subsystem.TryGetBoundaryPoints(Points)) continue;

                    if (BoundsOf(Points) is Rect rect) return rect;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlaySpaceBoundary] Could not read the boundary: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// The XZ rectangle around a boundary polygon, or null when the polygon cannot describe an
        /// area — fewer than three points, or a footprint too thin to stand in. Split out from the
        /// runtime query so the arithmetic can be checked without a headset.
        /// </summary>
        public static Rect? BoundsOf(IList<Vector3> points)
        {
            if (points == null || points.Count < 3) return null;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var point in points)
            {
                var xz = new Vector2(point.x, point.z);
                min = Vector2.Min(min, xz);
                max = Vector2.Max(max, xz);
            }

            var size = max - min;
            if (size.x < 0.5f || size.y < 0.5f) return null;

            return new Rect(min.x, min.y, size.x, size.y);
        }
    }
}

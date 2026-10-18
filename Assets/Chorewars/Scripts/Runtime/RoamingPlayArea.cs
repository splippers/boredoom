using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Makes sure the play area is big enough to walk a whole house in.
    /// </summary>
    /// <remarks>
    /// With no boundary and no room scan, the play area falls back to a box a few metres across, and
    /// the floor, the scoring and the tool are all clamped to it. Free roaming needs the opposite: no edge
    /// the player can reach. So the area is grown, centred where it already was, to at least
    /// <see cref="MinimumMeters"/> on each side. It is never made smaller, so a real, larger room is kept.
    /// </remarks>
    public static class RoamingPlayArea
    {
        public const float MinimumMeters = 16f;

        /// <summary>The floor texture is kept to about this many metres per texel, or coarser if it must be.</summary>
        public const float TargetTexelMeters = 0.06f;

        public static Bounds EnsureRoaming(Bounds area, float minimumMeters = MinimumMeters)
        {
            var size = area.size;
            size.x = Mathf.Max(size.x, minimumMeters);
            size.z = Mathf.Max(size.z, minimumMeters);
            return new Bounds(area.center, size);
        }

        /// <summary>Texture size for a floor this big: fine enough to see where you have been, never beyond what the visualizer allows.</summary>
        public static int FloorResolution(Bounds area, int minimum = 64, int maximum = 256)
        {
            float longest = Mathf.Max(area.size.x, area.size.z);
            int wanted = Mathf.CeilToInt(longest / TargetTexelMeters);
            return Mathf.Clamp(wanted, minimum, maximum);
        }
    }
}

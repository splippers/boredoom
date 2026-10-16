using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Where a doorway sits on one wall of one room: which wall, and the stretch of it to leave open.
    /// </summary>
    /// <remarks>
    /// The room rectangle and the crossing point are enough to place this, so no mesh and no scene
    /// object is involved; the dollhouse can be given the wall to open without knowing anything about
    /// how the room was scanned.
    /// </remarks>
    public struct WallGap
    {
        /// <summary>True when the wall runs along X (a run at constant Z); false for constant X.</summary>
        public bool alongX;

        /// <summary>The wall's constant coordinate: Z when <see cref="alongX"/>, otherwise X.</summary>
        public float offset;

        /// <summary>The span the wall covers on its running axis, clipped to the room.</summary>
        public float spanMin;
        public float spanMax;

        /// <summary>The stretch left open, on the running axis.</summary>
        public float gapMin;
        public float gapMax;

        public float GapCentre => (gapMin + gapMax) * 0.5f;

        public float GapWidth => gapMax - gapMin;
    }

    /// <summary>
    /// Turns "the player walked through here" into "leave this wall open at this spot".
    /// </summary>
    /// <remarks>
    /// A crossing is recorded on the boundary between two rooms, so it always lies on a wall of each
    /// room. This picks the nearest wall and centres the opening on the crossing, then pulls it back
    /// inside the wall: a door on a longer wall stays where the player went through, while a door near
    /// a corner is nudged along rather than hanging off the end of the wall. The result is the same
    /// wall for both rooms of the link, because both are measured from the same crossing.
    /// </remarks>
    public static class HouseDoorway
    {
        /// <summary>How wide a doorway is in the world, in metres. A normal single door.</summary>
        public const float DefaultDoorWidth = 0.9f;

        /// <summary>
        /// How far a crossing may sit from a wall and still count as being on it. A crossing in the
        /// middle of a room is not a doorway — there is no wall there — so it opens nothing.
        /// </summary>
        /// <remarks>
        /// A crossing is the midpoint of two consecutive head samples either side of a room boundary,
        /// so it lands within a few centimetres of the wall. Half a metre is generous tolerance for
        /// that while still refusing a crossing that is plainly in the middle of the floor: rooms are
        /// metres across, so half a metre is near the wall, not in the room.
        /// </remarks>
        public const float DefaultMaxEdgeDistance = 0.5f;

        public static bool TryFind(Rect room, Vector2 crossing, out WallGap gap,
            float doorWidth = DefaultDoorWidth, float maxEdgeDistance = DefaultMaxEdgeDistance)
        {
            gap = default;

            if (room.width <= 0f || room.height <= 0f) return false;
            if (doorWidth <= 0f) return false;

            float toMinX = crossing.x - room.xMin;
            float toMaxX = room.xMax - crossing.x;
            float toMinY = crossing.y - room.yMin;
            float toMaxY = room.yMax - crossing.y;

            float nearest = toMinX;
            int wall = 0;
            if (toMaxX < nearest) { nearest = toMaxX; wall = 1; }
            if (toMinY < nearest) { nearest = toMinY; wall = 2; }
            if (toMaxY < nearest) { nearest = toMaxY; wall = 3; }

            if (nearest < 0f || nearest > maxEdgeDistance) return false;

            // Walls 0/1 sit at constant X and run along Z; walls 2/3 sit at constant Z and run along X.
            gap.alongX = wall >= 2;
            if (gap.alongX)
            {
                gap.offset = wall == 2 ? room.yMin : room.yMax;
                gap.spanMin = room.xMin;
                gap.spanMax = room.xMax;
            }
            else
            {
                gap.offset = wall == 0 ? room.xMin : room.xMax;
                gap.spanMin = room.yMin;
                gap.spanMax = room.yMax;
            }

            float centre = gap.alongX ? crossing.x : crossing.y;
            float half = doorWidth * 0.5f;

            gap.gapMin = centre - half;
            gap.gapMax = centre + half;

            float span = gap.spanMax - gap.spanMin;

            // A wall shorter than the doorway has no room for anything but the doorway.
            if (span <= doorWidth)
            {
                gap.gapMin = gap.spanMin;
                gap.gapMax = gap.spanMax;
                return true;
            }

            // Pull the opening back inside the wall, keeping its width where there is space to.
            if (gap.gapMin < gap.spanMin)
            {
                gap.gapMax += gap.spanMin - gap.gapMin;
                gap.gapMin = gap.spanMin;
            }

            if (gap.gapMax > gap.spanMax)
            {
                gap.gapMin -= gap.gapMax - gap.spanMax;
                gap.gapMax = gap.spanMax;
            }

            return true;
        }
    }
}

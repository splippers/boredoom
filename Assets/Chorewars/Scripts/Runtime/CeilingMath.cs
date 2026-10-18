using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>The maths of finding a room's outline by looking at the corners of its ceiling.</summary>
    public static class CeilingMath
    {
        /// <summary>A gaze must tip at least this far above level to count as looking at the ceiling.</summary>
        public const float MinUpwardness = 0.15f;

        public const float DefaultCeilingMeters = 2.5f;

        /// <summary>Where a gaze meets the ceiling plane, or false if it is not pointing up enough to meet it.</summary>
        public static bool TryHit(Vector3 origin, Vector3 direction, float ceilingY, out Vector3 point)
        {
            point = default;
            if (direction.sqrMagnitude < 1e-6f) return false;
            direction.Normalize();
            if (direction.y < MinUpwardness || ceilingY <= origin.y) return false;

            point = origin + direction * ((ceilingY - origin.y) / direction.y);
            return true;
        }

        /// <summary>The smallest box (floor plan) holding all the corners.</summary>
        public static Rect Outline(IReadOnlyList<Vector3> corners)
        {
            if (corners == null || corners.Count == 0) return default;

            var min = new Vector2(corners[0].x, corners[0].z);
            var max = min;
            for (int i = 1; i < corners.Count; i++)
            {
                min = Vector2.Min(min, new Vector2(corners[i].x, corners[i].z));
                max = Vector2.Max(max, new Vector2(corners[i].x, corners[i].z));
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>True when the point is close enough to the first corner to mean "close the loop".</summary>
        public static bool ClosesLoop(IReadOnlyList<Vector3> corners, Vector3 point, float withinMeters = 0.5f)
        {
            if (corners == null || corners.Count < 3) return false;
            var d = new Vector2(corners[0].x - point.x, corners[0].z - point.z);
            return d.magnitude <= withinMeters;
        }
    }
}

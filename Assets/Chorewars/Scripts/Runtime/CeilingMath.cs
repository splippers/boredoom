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

        /// <summary>
        /// The ceiling's height from looking at the same spot from two places: where the two gaze rays pass closest.
        /// Fails if the places are under <paramref name="minBaseline"/> metres apart (sideways) or the rays are near parallel.
        /// </summary>
        public static bool TryHeightFromTwoGazes(Vector3 originA, Vector3 dirA, Vector3 originB, Vector3 dirB, out float height, float minBaseline = 1.0f)
        {
            height = 0f;
            dirA.Normalize(); dirB.Normalize();
            var flat = new Vector2(originB.x - originA.x, originB.z - originA.z);
            if (flat.magnitude < minBaseline) return false;

            var w = originA - originB;
            float b = Vector3.Dot(dirA, dirB), d = Vector3.Dot(dirA, w), e = Vector3.Dot(dirB, w);
            float denom = 1f - b * b;
            if (denom < 1e-3f) return false;

            float sA = (b * e - d) / denom, sB = (e - b * d) / denom;
            if (sA <= 0f || sB <= 0f) return false;

            var pointA = originA + dirA * sA;
            var pointB = originB + dirB * sB;
            if (Vector3.Distance(pointA, pointB) > 0.5f) return false;   // the two looks were not at the same spot

            height = (pointA.y + pointB.y) * 0.5f;
            return height > 1.8f && height < 6f;
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

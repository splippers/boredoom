using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>Pure geometry for the one-room hoover slice: cells inside a floor polygon (XZ plane) and which cells a tool point cleans.</summary>
    public static class HooverGrid
    {
        public static bool Contains(IReadOnlyList<Vector3> poly, float x, float z)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = poly[i]; var b = poly[j];
                if ((a.z > z) != (b.z > z) && x < (b.x - a.x) * (z - a.z) / (b.z - a.z) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Centres (x,z) of every cell of the given size whose centre lies inside the polygon.</summary>
        public static List<Vector2> Cells(IReadOnlyList<Vector3> poly, float size)
        {
            var cells = new List<Vector2>();
            if (poly == null || poly.Count < 3 || size <= 0.01f) return cells;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in poly) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            for (float z = minZ + size * 0.5f; z < maxZ; z += size)
                for (float x = minX + size * 0.5f; x < maxX; x += size)
                    if (Contains(poly, x, z)) cells.Add(new Vector2(x, z));
            return cells;
        }

        /// <summary>Marks cells within radius of (x,z) as clean; returns how many were newly cleaned.</summary>
        public static int Clean(IReadOnlyList<Vector2> cells, bool[] clean, float x, float z, float radius)
        {
            int n = 0; float r2 = radius * radius;
            for (int i = 0; i < cells.Count; i++)
            {
                if (clean[i]) continue;
                float dx = cells[i].x - x, dz = cells[i].y - z;
                if (dx * dx + dz * dz <= r2) { clean[i] = true; n++; }
            }
            return n;
        }
    }
}

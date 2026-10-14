using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.AR
{
    public class CoverageMap : MonoBehaviour
    {
        [SerializeField] private float cellSizeMeters = 0.25f;
        [SerializeField] private bool useFixedBounds = true;
        [SerializeField] private Vector2 fixedMinXZ = new(-2f, -2f);
        [SerializeField] private Vector2 fixedMaxXZ = new(2f, 2f);

        /// <summary>Raised with the world-space centre of a cell the first time it becomes visited.</summary>
        /// <remarks>
        /// Lets presentation layers (juice, audio, hints) react to genuinely new progress instead of
        /// polling the whole map every frame. Only fires once per cell for the lifetime of the map.
        /// </remarks>
        public event Action<Vector3> CellFirstVisited;

        /// <summary>Edge length of one coverage cell, in metres.</summary>
        public float CellSizeMeters => cellSizeMeters;

        private readonly HashSet<Vector2Int> _visited = new();
        private Vector2 _min;
        private Vector2 _max;

        private void Awake()
        {
            _min = fixedMinXZ;
            _max = fixedMaxXZ;
        }

        /// <summary>
        /// Override the fixed coverage bounds at runtime (e.g. from a room provider).
        /// Keeps coverage % meaningful when the play area is not the default ±2 m box.
        /// </summary>
        public void SetBounds(Vector2 minXZ, Vector2 maxXZ)
        {
            useFixedBounds = true;
            fixedMinXZ = minXZ;
            fixedMaxXZ = maxXZ;
            _min = minXZ;
            _max = maxXZ;
        }

        public void MarkVisitedWorldPosition(Vector3 worldPos)
        {
            var xz = new Vector2(worldPos.x, worldPos.z);
            if (!useFixedBounds)
            {
                _min = Vector2.Min(_min, xz);
                _max = Vector2.Max(_max, xz);
            }

            var cell = WorldToCell(xz);
            if (!_visited.Add(cell)) return;

            CellFirstVisited?.Invoke(CellCentreWorld(cell, worldPos.y));
        }

        /// <summary>World-space centre of a cell, keeping the caller's height.</summary>
        private Vector3 CellCentreWorld(Vector2Int cell, float y)
        {
            return new Vector3(
                cell.x * cellSizeMeters + cellSizeMeters * 0.5f,
                y,
                cell.y * cellSizeMeters + cellSizeMeters * 0.5f);
        }

        public float ComputeCoveragePercent()
        {
            if (cellSizeMeters <= 0f) return 0f;

            var minCell = WorldToCell(_min);
            var maxCell = WorldToCell(_max);

            int width = Mathf.Abs(maxCell.x - minCell.x) + 1;
            int height = Mathf.Abs(maxCell.y - minCell.y) + 1;
            int total = Mathf.Max(1, width * height);

            return Mathf.Clamp01(_visited.Count / (float)total) * 100f;
        }

        /// <summary>
        /// Compute coverage percentage restricted to a 2D axis-aligned rectangle (XZ world space).
        /// A cell belongs to the rect iff its centre lies inside it; the same test is used for the
        /// numerator and denominator so a fully cleaned room reads exactly 100% (an index-span
        /// denominator would leave it short of the dollhouse "clean" threshold).
        /// </summary>
        public float ComputeCoveragePercent(Rect rectXZ)
        {
            if (cellSizeMeters <= 0f) return 0f;
            if (rectXZ.width <= 0f || rectXZ.height <= 0f) return 0f;

            var minCell = WorldToCell(new Vector2(rectXZ.xMin, rectXZ.yMin));
            var maxCell = WorldToCell(new Vector2(rectXZ.xMax, rectXZ.yMax));

            int totalInRect = 0;
            int visitedInRect = 0;

            for (int cx = minCell.x; cx <= maxCell.x; cx++)
            {
                float wx = cx * cellSizeMeters + cellSizeMeters * 0.5f;
                if (wx < rectXZ.xMin || wx > rectXZ.xMax) continue;

                for (int cz = minCell.y; cz <= maxCell.y; cz++)
                {
                    float wz = cz * cellSizeMeters + cellSizeMeters * 0.5f;
                    if (wz < rectXZ.yMin || wz > rectXZ.yMax) continue;

                    totalInRect++;
                    if (_visited.Contains(new Vector2Int(cx, cz))) visitedInRect++;
                }
            }

            if (totalInRect == 0) return 0f;
            return Mathf.Clamp01(visitedInRect / (float)totalInRect) * 100f;
        }

        private Vector2Int WorldToCell(Vector2 xz)
        {
            int cx = Mathf.FloorToInt(xz.x / cellSizeMeters);
            int cz = Mathf.FloorToInt(xz.y / cellSizeMeters);
            return new Vector2Int(cx, cz);
        }
    }
}

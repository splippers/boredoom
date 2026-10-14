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

        // Scratch buffers for the unvisited-cluster search, reused between calls so the
        // missed-spot hint never allocates.
        private int[] _clusterSeen;
        private int[] _clusterQueue;

        // Cached in-bounds visited count for ComputeCoveragePercent (see there for why).
        private int _cachedInsideCount = -1;
        private int _cachedVisitedStamp = -1;

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
            _cachedInsideCount = -1;
        }

        public void MarkVisitedWorldPosition(Vector3 worldPos)
        {
            var xz = new Vector2(worldPos.x, worldPos.z);
            if (!useFixedBounds)
            {
                var newMin = Vector2.Min(_min, xz);
                var newMax = Vector2.Max(_max, xz);

                if (newMin != _min || newMax != _max)
                {
                    _min = newMin;
                    _max = newMax;
                    _cachedInsideCount = -1;
                }
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

            // Count only cells inside the bounds. _visited can hold cells the tool reached outside
            // the play area; counting those reported 100% while the middle was still dirty.
            // Cached on _visited.Count because cells are only ever added, so an unchanged count
            // means an unchanged set — keeps the per-frame cost O(1) instead of O(span).
            if (_cachedInsideCount < 0 || _cachedVisitedStamp != _visited.Count)
            {
                _cachedVisitedStamp = _visited.Count;
                _cachedInsideCount = 0;

                int originX = Mathf.Min(minCell.x, maxCell.x);
                int originZ = Mathf.Min(minCell.y, maxCell.y);

                for (int cx = 0; cx < width; cx++)
                {
                    for (int cz = 0; cz < height; cz++)
                    {
                        if (_visited.Contains(new Vector2Int(originX + cx, originZ + cz))) _cachedInsideCount++;
                    }
                }
            }

            return Mathf.Clamp01(_cachedInsideCount / (float)total) * 100f;
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

        /// <summary>How many cells the current bounds cover. Useful for scaling hint markers.</summary>
        public int BoundsCellCount
        {
            get
            {
                var minCell = WorldToCell(_min);
                var maxCell = WorldToCell(_max);
                return Mathf.Max(0, Mathf.Abs(maxCell.x - minCell.x) + 1) *
                       Mathf.Max(0, Mathf.Abs(maxCell.y - minCell.y) + 1);
            }
        }

        /// <summary>
        /// Finds the largest connected (4-way) group of still-unvisited cells and returns its
        /// approximate world-space centre and cell count. Feeds the "you missed a bit" hint.
        /// </summary>
        /// <returns>
        /// False when there is nothing worth pointing at — everything cleaned, or only scattered
        /// singletons smaller than <paramref name="minCells"/>.
        /// </returns>
        public bool TryFindLargestUnvisitedCluster(out Vector3 worldCentre, out int cellCount, int minCells = 6)
        {
            worldCentre = Vector3.zero;
            cellCount = 0;

            if (cellSizeMeters <= 0f) return false;

            // Compute the same bounding box of cells that coverage uses, normalised so origin is
            // the low corner (min/max can arrive in either order).
            var a = WorldToCell(_min);
            var b = WorldToCell(_max);

            int originX = Mathf.Min(a.x, b.x);
            int originZ = Mathf.Min(a.y, b.y);
            int width = Mathf.Abs(b.x - a.x) + 1;
            int height = Mathf.Abs(b.y - a.y) + 1;
            int total = width * height;

            if (total <= 0) return false;

            if (_clusterSeen == null || _clusterSeen.Length < total)
            {
                _clusterSeen = new int[total];
                _clusterQueue = new int[total];
            }
            else
            {
                Array.Clear(_clusterSeen, 0, total);
            }

            int bestCount = 0;
            long bestSumX = 0;
            long bestSumZ = 0;

            for (int cz = 0; cz < height; cz++)
            {
                for (int cx = 0; cx < width; cx++)
                {
                    int startIndex = cx + cz * width;

                    if (_clusterSeen[startIndex] != 0) continue;

                    int startWorldX = originX + cx;
                    int startWorldZ = originZ + cz;

                    if (_visited.Contains(new Vector2Int(startWorldX, startWorldZ)))
                    {
                        _clusterSeen[startIndex] = -1;
                        continue;
                    }

                    // Breadth-first flood fill of the connected run. The neighbour test lives in a
                    // static helper rather than a local function: a local function closing over
                    // `tail` would allocate a closure per cell, which is exactly what we are
                    // trying to avoid.
                    int head = 0;
                    int tail = 0;
                    _clusterQueue[tail++] = startIndex;
                    _clusterSeen[startIndex] = 1;

                    int count = 0;
                    long sumX = 0;
                    long sumZ = 0;

                    while (head < tail)
                    {
                        int current = _clusterQueue[head++];
                        int localX = current % width;
                        int localZ = current / width;

                        count++;
                        sumX += originX + localX;
                        sumZ += originZ + localZ;

                        if (localX > 0)
                            EnqueueNeighbour(localX - 1, localZ, width, originX, originZ, _visited, _clusterSeen, _clusterQueue, ref tail);

                        if (localX < width - 1)
                            EnqueueNeighbour(localX + 1, localZ, width, originX, originZ, _visited, _clusterSeen, _clusterQueue, ref tail);

                        if (localZ > 0)
                            EnqueueNeighbour(localX, localZ - 1, width, originX, originZ, _visited, _clusterSeen, _clusterQueue, ref tail);

                        if (localZ < height - 1)
                            EnqueueNeighbour(localX, localZ + 1, width, originX, originZ, _visited, _clusterSeen, _clusterQueue, ref tail);
                    }

                    if (count > bestCount)
                    {
                        bestCount = count;
                        bestSumX = sumX;
                        bestSumZ = sumZ;
                    }
                }
            }

            if (bestCount < Mathf.Max(1, minCells)) return false;

            cellCount = bestCount;
            worldCentre = new Vector3(
                (bestSumX / (float)bestCount) * cellSizeMeters + cellSizeMeters * 0.5f,
                0f,
                (bestSumZ / (float)bestCount) * cellSizeMeters + cellSizeMeters * 0.5f);
            return true;
        }

        /// <summary>Marks one neighbour as seen and queues it if it is inside bounds and unvisited.</summary>
        private static void EnqueueNeighbour(
            int localX, int localZ, int width, int originX, int originZ,
            HashSet<Vector2Int> visited, int[] seen, int[] queue, ref int tail)
        {
            int index = localX + localZ * width;
            if (seen[index] != 0) return;

            if (visited.Contains(new Vector2Int(originX + localX, originZ + localZ)))
            {
                seen[index] = -1;
                return;
            }

            seen[index] = 1;
            queue[tail++] = index;
        }

        private Vector2Int WorldToCell(Vector2 xz)
        {
            int cx = Mathf.FloorToInt(xz.x / cellSizeMeters);
            int cz = Mathf.FloorToInt(xz.y / cellSizeMeters);
            return new Vector2Int(cx, cz);
        }
    }
}

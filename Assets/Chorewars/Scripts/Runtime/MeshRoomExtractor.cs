using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Splits raw scanned geometry into rooms, without depending on a room-understanding SDK.
    /// </summary>
    /// <remarks>
    /// This is a heuristic, and it is here because the project builds against plain OpenXR with no
    /// Meta XR / MRUK package, so nothing hands us room anchors. When MRUK is available it should
    /// replace this — the pipeline only needs <see cref="IScanSource"/>, so the swap is local.
    ///
    /// The method, in short:
    /// <list type="number">
    /// <item>Find the floor: the height band holding the most vertices. Floors are the densest thing
    /// a room scan produces, so the busiest band is the floor far more often than not.</item>
    /// <item>Rasterise the floor band onto a grid — those are the walkable cells.</item>
    /// <item>Rasterise everything well above the floor as wall, and thicken it. A doorway is a gap in
    /// a wall, and left alone a floor grid leaks straight through it, which merges every room in the
    /// house into one blob. Thickening the walls closes gaps up to doorway width.</item>
    /// <item>Flood-fill the walkable cells the thickened walls leave free. Each connected region is a
    /// room; its axis-aligned footprint becomes the <see cref="RoomScan"/>.</item>
    /// </list>
    /// An open-plan space with no walls comes back as a single room, which is an honest answer rather
    /// than a wrong one.
    /// </remarks>
    public static class MeshRoomExtractor
    {
        /// <summary>A connected walkable region, in grid cells.</summary>
        private struct CellRegion
        {
            public int MinX;
            public int MinZ;
            public int MaxX;
            public int MaxZ;
        }

        /// <summary>Extracts room footprints from scanned world-space vertices.</summary>
        public static List<RoomScan> Extract(
            IReadOnlyList<Vector3> worldVertices,
            float cellSizeMeters = 0.2f,
            float floorBandMeters = 0.15f,
            float wallHeightMeters = 0.4f,
            float doorwayCloseMeters = 0.5f,
            int minimumCellsPerRoom = 8,
            int maximumRooms = 12)
        {
            var rooms = new List<RoomScan>();
            if (worldVertices == null || worldVertices.Count < 16) return rooms;

            float floorY = FindFloorY(worldVertices);

            float cell = Mathf.Max(0.05f, cellSizeMeters);

            // Footprint of the whole scan, from the floor band only (walls can overhang).
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < worldVertices.Count; i++)
            {
                var v = worldVertices[i];
                if (Mathf.Abs(v.y - floorY) > floorBandMeters) continue;
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.z < minZ) minZ = v.z;
                if (v.z > maxZ) maxZ = v.z;
            }

            if (minX > maxX || minZ > maxZ) return rooms;

            int width = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / cell));
            int depth = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / cell));

            var floor = new bool[width * depth];
            var wall = new bool[width * depth];

            for (int i = 0; i < worldVertices.Count; i++)
            {
                var v = worldVertices[i];
                int cx = Mathf.Clamp((int)((v.x - minX) / cell), 0, width - 1);
                int cz = Mathf.Clamp((int)((v.z - minZ) / cell), 0, depth - 1);
                int index = cz * width + cx;

                if (Mathf.Abs(v.y - floorY) <= floorBandMeters) floor[index] = true;
                else if (v.y - floorY >= wallHeightMeters) wall[index] = true;
            }

            int closeCells = Mathf.Max(0, Mathf.RoundToInt(doorwayCloseMeters / cell));

            // Close doorways by thickening the walls — but back off if that leaves nothing usable.
            // A small room can be eaten entirely by a doorway-sized wall thickness, and returning no
            // rooms at all is worse than returning one coarse one.
            var regions = new List<CellRegion>();
            int usedCloseCells = 0;

            for (int radius = closeCells; radius >= 0; radius--)
            {
                var blocked = radius > 0 ? Dilate(wall, width, depth, radius) : wall;
                var found = Label(OpenCells(floor, blocked), width, depth, minimumCellsPerRoom);

                if (found.Count <= 0) continue;

                regions = found;
                usedCloseCells = radius;
                break;
            }

            // Thickening the walls to close doorways also eats that much floor off every room edge.
            // Grow each footprint back by the same amount so the reported room is the room, not the
            // room minus half a metre all round — then clamp to the scan.
            if (usedCloseCells > 0)
            {
                for (int i = 0; i < regions.Count; i++)
                {
                    var region = regions[i];
                    region.MinX = Mathf.Max(0, region.MinX - usedCloseCells);
                    region.MinZ = Mathf.Max(0, region.MinZ - usedCloseCells);
                    region.MaxX = Mathf.Min(width - 1, region.MaxX + usedCloseCells);
                    region.MaxZ = Mathf.Min(depth - 1, region.MaxZ + usedCloseCells);
                    regions[i] = region;
                }
            }

            // Order by footprint so room ids are stable for a given scan.
            regions.Sort((a, b) =>
            {
                if (a.MinZ != b.MinZ) return a.MinZ.CompareTo(b.MinZ);
                return a.MinX.CompareTo(b.MinX);
            });

            if (regions.Count > maximumRooms) regions.RemoveRange(maximumRooms, regions.Count - maximumRooms);

            for (int i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                var bounds = new Rect(
                    minX + region.MinX * cell,
                    minZ + region.MinZ * cell,
                    (region.MaxX - region.MinX + 1) * cell,
                    (region.MaxZ - region.MinZ + 1) * cell);

                rooms.Add(new RoomScan
                {
                    roomId = $"Room-{rooms.Count + 1:D3}",
                    boundsXZ = bounds,
                    floorY = floorY,
                    wallHeight = WallHeightFor(worldVertices, bounds, floorY)
                });
            }

            return rooms;
        }

        /// <summary>The floor is the busiest height band. Walls spread their vertices over the full height.</summary>
        private static float FindFloorY(IReadOnlyList<Vector3> vertices)
        {
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < vertices.Count; i++)
            {
                float y = vertices[i].y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            // A flat scan has no height range at all. That is not an error: a floor-only capture is a
            // legitimate — if unusual — result, and its floor is simply the plane it is all sitting on.
            float bin = Mathf.Max(0.03f, (maxY - minY) / 40f);
            var counts = new Dictionary<int, int>();
            int bestBin = 0;
            int bestCount = 0;

            for (int i = 0; i < vertices.Count; i++)
            {
                int key = Mathf.FloorToInt((vertices[i].y - minY) / bin + 0.5f);
                counts.TryGetValue(key, out int count);
                count++;
                counts[key] = count;

                if (count > bestCount)
                {
                    bestCount = count;
                    bestBin = key;
                }
            }

            return minY + bestBin * bin;
        }

        /// <summary>Cells that are marked and not blocked.</summary>
        private static bool[] OpenCells(bool[] marked, bool[] blocked)
        {
            var result = new bool[marked.Length];
            for (int i = 0; i < result.Length; i++) result[i] = marked[i] && !blocked[i];
            return result;
        }

        private static bool[] Dilate(bool[] source, int width, int depth, int radius)
        {
            var result = new bool[source.Length];

            for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
            {
                if (!source[z * width + x]) continue;

                int x0 = Mathf.Max(0, x - radius), x1 = Mathf.Min(width - 1, x + radius);
                int z0 = Mathf.Max(0, z - radius), z1 = Mathf.Min(depth - 1, z + radius);

                for (int zz = z0; zz <= z1; zz++)
                for (int xx = x0; xx <= x1; xx++)
                    result[zz * width + xx] = true;
            }

            return result;
        }

        /// <summary>Connected-component labelling (4-neighbour) over the walkable cells.</summary>
        private static List<CellRegion> Label(bool[] walkable, int width, int depth, int minimumCells)
        {
            var regions = new List<CellRegion>();
            var seen = new bool[walkable.Length];
            var queue = new Queue<int>();

            for (int start = 0; start < walkable.Length; start++)
            {
                if (!walkable[start] || seen[start]) continue;

                var region = new CellRegion
                {
                    MinX = int.MaxValue,
                    MinZ = int.MaxValue,
                    MaxX = int.MinValue,
                    MaxZ = int.MinValue
                };
                int count = 0;

                seen[start] = true;
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    int x = index % width;
                    int z = index / width;

                    if (x < region.MinX) region.MinX = x;
                    if (x > region.MaxX) region.MaxX = x;
                    if (z < region.MinZ) region.MinZ = z;
                    if (z > region.MaxZ) region.MaxZ = z;
                    count++;

                    Enqueue(queue, seen, walkable, width, depth, x - 1, z);
                    Enqueue(queue, seen, walkable, width, depth, x + 1, z);
                    Enqueue(queue, seen, walkable, width, depth, x, z - 1);
                    Enqueue(queue, seen, walkable, width, depth, x, z + 1);
                }

                if (count >= minimumCells) regions.Add(region);
            }

            return regions;
        }

        private static void Enqueue(Queue<int> queue, bool[] seen, bool[] walkable, int width, int depth, int x, int z)
        {
            if (x < 0 || z < 0 || x >= width || z >= depth) return;

            int index = z * width + x;
            if (seen[index] || !walkable[index]) return;

            seen[index] = true;
            queue.Enqueue(index);
        }

        /// <summary>Median-ish wall height: the tallest vertices inside the room footprint, clamped.</summary>
        private static float WallHeightFor(IReadOnlyList<Vector3> vertices, Rect footprint, float floorY)
        {
            float top = 0f;

            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                if (!footprint.Contains(new Vector2(v.x, v.z))) continue;
                float height = v.y - floorY;
                if (height > top) top = height;
            }

            return Mathf.Clamp(top, 1.5f, 3.2f);
        }
    }
}

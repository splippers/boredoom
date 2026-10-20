using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Chorewars.Runtime
{
    public enum FloorCell : byte { Unknown = 0, Free = 1, Blocked = 2 }

    /// <summary>
    /// A room's floor as a grid of squares, each unknown, walked clear, or blocked by furniture, so the game
    /// knows how much of the floor can really be cleaned. Pure data: the scanner and the tests drive it.
    /// </summary>
    public sealed class FloorGrid
    {
        public readonly Rect Bounds;
        public readonly float CellSize;
        public readonly int Columns;
        public readonly int Rows;
        readonly FloorCell[] _cells;

        public FloorGrid(Rect bounds, float cellSize = 0.25f)
        {
            Bounds = bounds;
            CellSize = Mathf.Max(0.05f, cellSize);
            Columns = Mathf.Max(1, Mathf.CeilToInt(bounds.width / CellSize));
            Rows = Mathf.Max(1, Mathf.CeilToInt(bounds.height / CellSize));
            _cells = new FloorCell[Columns * Rows];
        }

        public FloorCell this[int column, int row] => _cells[row * Columns + column];

        public bool TryCell(Vector2 point, out int column, out int row)
        {
            column = Mathf.FloorToInt((point.x - Bounds.xMin) / CellSize);
            row = Mathf.FloorToInt((point.y - Bounds.yMin) / CellSize);
            return column >= 0 && column < Columns && row >= 0 && row < Rows;
        }

        public Vector2 CentreOf(int column, int row) =>
            new Vector2(Bounds.xMin + (column + 0.5f) * CellSize, Bounds.yMin + (row + 0.5f) * CellSize);

        /// <summary>Marks unknown cells within <paramref name="radius"/> of the point as walked clear. Blocked cells stay blocked. Returns how many changed.</summary>
        public int MarkWalked(Vector2 point, float radius)
        {
            int changed = 0;
            int reach = Mathf.CeilToInt(radius / CellSize);
            if (!TryCell(point, out int c0, out int r0))
            {
                c0 = Mathf.Clamp(Mathf.FloorToInt((point.x - Bounds.xMin) / CellSize), 0, Columns - 1);
                r0 = Mathf.Clamp(Mathf.FloorToInt((point.y - Bounds.yMin) / CellSize), 0, Rows - 1);
            }

            for (int r = Mathf.Max(0, r0 - reach); r <= Mathf.Min(Rows - 1, r0 + reach); r++)
            for (int c = Mathf.Max(0, c0 - reach); c <= Mathf.Min(Columns - 1, c0 + reach); c++)
            {
                if (_cells[r * Columns + c] != FloorCell.Unknown) continue;
                if ((CentreOf(c, r) - point).magnitude > radius) continue;
                _cells[r * Columns + c] = FloorCell.Free;
                changed++;
            }

            return changed;
        }

        /// <summary>Marks every cell whose centre is inside the polygon as blocked (furniture wins over a walk). Returns how many changed.</summary>
        public int MarkBlocked(IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 0;

            int changed = 0;
            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                if (_cells[r * Columns + c] == FloorCell.Blocked) continue;
                if (!Contains(polygon, CentreOf(c, r))) continue;
                _cells[r * Columns + c] = FloorCell.Blocked;
                changed++;
            }

            return changed;
        }

        public int Count(FloorCell state)
        {
            int n = 0;
            foreach (var cell in _cells) if (cell == state) n++;
            return n;
        }

        public float AreaOf(FloorCell state) => Count(state) * CellSize * CellSize;

        /// <summary>One character per cell ('.' unknown, 'o' free, '#' blocked), rows from the lowest z, separated by '/'.</summary>
        public string Encode()
        {
            var sb = new StringBuilder(_cells.Length + Rows);
            for (int r = 0; r < Rows; r++)
            {
                if (r > 0) sb.Append('/');
                for (int c = 0; c < Columns; c++)
                    sb.Append(_cells[r * Columns + c] == FloorCell.Free ? 'o' : _cells[r * Columns + c] == FloorCell.Blocked ? '#' : '.');
            }

            return sb.ToString();
        }

        public static bool Contains(IReadOnlyList<Vector2> polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            return inside;
        }
    }
}

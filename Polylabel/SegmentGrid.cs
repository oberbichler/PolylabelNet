using System.Buffers;
using System.Runtime.CompilerServices;

namespace Polylabel;

/// <summary>
/// A uniform grid over all segments of a polygon, used to answer the signed distance queries
/// of the search without touching every segment.
/// </summary>
/// <remarks>
/// <para>
/// The search evaluates the signed distance a few hundred to a few thousand times, and the plain
/// implementation walks every segment of every ring each time. Building an index once per search
/// costs O(segments) - about the same as a single probe - and cuts the segments actually examined
/// to a few percent.
/// </para>
/// <para>
/// The results are identical to the linear scan, bit for bit. The distance is the minimum over a
/// set of segments, and a minimum does not depend on the order it is taken in; the grid only
/// leaves out segments that provably cannot beat the current best, by expanding cell rings until
/// the covered radius exceeds it. The inside/outside parity stays exact because a generation
/// stamp guarantees that each segment is tested exactly once per query, even though a segment can
/// be registered in several cells.
/// </para>
/// <para>
/// All buffers come from <see cref="ArrayPool{T}"/> and are returned in <see cref="Dispose"/>, so
/// a warm application allocates next to nothing per search.
/// </para>
/// </remarks>
internal sealed class SegmentGrid : IDisposable
{
    /// <summary>
    /// Below this many segments the linear scan wins: it needs no index and no memory, and the
    /// scan is short enough that the indexing overhead would dominate.
    /// </summary>
    internal const int MinimumSegmentsForGrid = 512;

    private readonly double[] _ax, _ay, _bx, _by;
    private readonly int[] _cellStart, _cellItems, _stamp;
    private readonly int _segmentCount, _entryCount, _cols, _rows;
    private readonly double _minX, _minY, _cellSize;
    private int _generation;

    private SegmentGrid(
        double[] ax, double[] ay, double[] bx, double[] by, int segmentCount,
        int[] cellStart, int[] cellItems, int entryCount, int[] stamp,
        double minX, double minY, double cellSize, int cols, int rows)
    {
        _ax = ax; _ay = ay; _bx = bx; _by = by; _segmentCount = segmentCount;
        _cellStart = cellStart; _cellItems = cellItems; _entryCount = entryCount; _stamp = stamp;
        _minX = minX; _minY = minY; _cellSize = cellSize; _cols = cols; _rows = rows;
    }

    /// <summary>
    /// Builds an index for the polygon, or returns null when the polygon is too small for the
    /// index to pay for itself.
    /// </summary>
    internal static SegmentGrid? TryBuild<TPolygon, TPoint>(TPolygon polygon)
        where TPolygon : struct, IPolygon<TPoint>
        where TPoint : struct, IPoint
    {
        int ringCount = polygon.RingCount;
        int segmentCount = 0;
        for (int r = 0; r < ringCount; r++)
        {
            segmentCount += polygon.GetRing(r).Length;
        }

        if (segmentCount < MinimumSegmentsForGrid)
        {
            return null;
        }

        double[] ax = ArrayPool<double>.Shared.Rent(segmentCount);
        double[] ay = ArrayPool<double>.Shared.Rent(segmentCount);
        double[] bx = ArrayPool<double>.Shared.Rent(segmentCount);
        double[] by = ArrayPool<double>.Shared.Rent(segmentCount);

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        double lengthSum = 0;

        int n = 0;
        for (int r = 0; r < ringCount; r++)
        {
            ReadOnlySpan<TPoint> ring = polygon.GetRing(r);
            int len = ring.Length;
            if (len == 0) continue;

            TPoint b = ring[len - 1];
            for (int i = 0; i < len; i++)
            {
                TPoint a = ring[i];
                double axi = a.X, ayi = a.Y, bxi = b.X, byi = b.Y;
                ax[n] = axi; ay[n] = ayi; bx[n] = bxi; by[n] = byi;
                n++;

                if (axi < minX) minX = axi;
                if (axi > maxX) maxX = axi;
                if (ayi < minY) minY = ayi;
                if (ayi > maxY) maxY = ayi;
                lengthSum += Math.Abs(bxi - axi) + Math.Abs(byi - ayi);

                b = a;
            }
        }

        double width = Math.Max(maxX - minX, double.Epsilon);
        double height = Math.Max(maxY - minY, double.Epsilon);
        int dim = Math.Max(1, (int)Math.Sqrt(n));
        double cellSize = Math.Max(width / dim, height / dim);

        // Cells must not be much smaller than a typical segment, or every segment lands in many
        // cells and the index grows superlinearly. A star shaped polygon with long spikes went
        // from 268 index entries per segment to 2 with this rule; for the usual dense outlines it
        // changes nothing. Where it does apply the grid coarsens until the search degenerates
        // gracefully into the linear scan it replaces.
        double averageSegmentLength = lengthSum / n;
        if (averageSegmentLength > cellSize) cellSize = averageSegmentLength;

        int cols = Math.Max(1, (int)(width / cellSize) + 1);
        int rows = Math.Max(1, (int)(height / cellSize) + 1);
        int cellCount = cols * rows;

        int[] cellStart = ArrayPool<int>.Shared.Rent(cellCount + 1);
        Array.Clear(cellStart, 0, cellCount + 1);

        for (int s = 0; s < n; s++)
        {
            CellRange(ax[s], ay[s], bx[s], by[s], minX, minY, cellSize, cols, rows,
                out int c0, out int c1, out int r0, out int r1);
            for (int r = r0; r <= r1; r++)
            {
                int rowBase = r * cols;
                for (int c = c0; c <= c1; c++) cellStart[rowBase + c + 1]++;
            }
        }

        for (int i = 1; i <= cellCount; i++) cellStart[i] += cellStart[i - 1];
        int entryCount = cellStart[cellCount];

        int[] cellItems = ArrayPool<int>.Shared.Rent(entryCount);
        int[] cursor = ArrayPool<int>.Shared.Rent(cellCount);
        Array.Copy(cellStart, cursor, cellCount);

        for (int s = 0; s < n; s++)
        {
            CellRange(ax[s], ay[s], bx[s], by[s], minX, minY, cellSize, cols, rows,
                out int c0, out int c1, out int r0, out int r1);
            for (int r = r0; r <= r1; r++)
            {
                int rowBase = r * cols;
                for (int c = c0; c <= c1; c++) cellItems[cursor[rowBase + c]++] = s;
            }
        }

        ArrayPool<int>.Shared.Return(cursor);

        // Rented arrays carry whatever the previous user left behind, which could collide with the
        // generation counter and silently skip segments. Clearing once here keeps the per-query
        // stamping sound.
        int[] stamp = ArrayPool<int>.Shared.Rent(n);
        Array.Clear(stamp, 0, n);

        return new SegmentGrid(ax, ay, bx, by, n, cellStart, cellItems, entryCount, stamp,
            minX, minY, cellSize, cols, rows);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CellRange(
        double ax, double ay, double bx, double by,
        double minX, double minY, double cellSize, int cols, int rows,
        out int c0, out int c1, out int r0, out int r1)
    {
        double loX = ax < bx ? ax : bx, hiX = ax < bx ? bx : ax;
        double loY = ay < by ? ay : by, hiY = ay < by ? by : ay;

        c0 = Clamp((int)((loX - minX) / cellSize), cols);
        c1 = Clamp((int)((hiX - minX) / cellSize), cols);
        r0 = Clamp((int)((loY - minY) / cellSize), rows);
        r1 = Clamp((int)((hiY - minY) / cellSize), rows);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Clamp(int value, int count) => value < 0 ? 0 : (value >= count ? count - 1 : value);

    /// <summary>
    /// Signed distance from the point to the polygon outline, negative outside. Identical to the
    /// linear scan down to the last bit.
    /// </summary>
    internal double SignedDistance(double px, double py)
    {
        int pc = Clamp((int)((px - _minX) / _cellSize), _cols);
        int pr = Clamp((int)((py - _minY) / _cellSize), _rows);

        // Parity: only segments in the cell row of py that reach to the right of px can cross the
        // ray. Every other segment fails the test anyway, so leaving them out is exact. The scan
        // starts one column early, so that rounding in the cell assignment can never drop a
        // segment that sits right at the boundary of the point's own cell.
        int generation = ++_generation;
        bool inside = false;
        int rowBase = pr * _cols;
        for (int c = pc > 0 ? pc - 1 : 0; c < _cols; c++)
        {
            int cell = rowBase + c;
            int end = _cellStart[cell + 1];
            for (int i = _cellStart[cell]; i < end; i++)
            {
                int s = _cellItems[i];
                if (_stamp[s] == generation) continue;
                _stamp[s] = generation;

                double ay = _ay[s], by = _by[s];
                if ((ay > py) != (by > py))
                {
                    double ax = _ax[s];
                    if (px < (_bx[s] - ax) * (py - ay) / (by - ay) + ax) inside = !inside;
                }
            }
        }

        // Distance: expand square rings of cells. After ring k every cell not yet looked at is at
        // least (k - 1) cell sizes away, so once the best distance is below that bound nothing
        // left can improve it.
        generation = ++_generation;
        double best = double.PositiveInfinity;
        int maxRing = Math.Max(Math.Max(pc, _cols - 1 - pc), Math.Max(pr, _rows - 1 - pr));

        for (int ring = 0; ring <= maxRing; ring++)
        {
            if (ring > 0)
            {
                double covered = (ring - 1) * _cellSize;
                if (covered > 0 && best <= covered * covered) break;
            }

            int c0 = pc - ring, c1 = pc + ring, r0 = pr - ring, r1 = pr + ring;
            for (int r = r0; r <= r1; r++)
            {
                if (r < 0 || r >= _rows) continue;
                bool edgeRow = r == r0 || r == r1;
                int rowOffset = r * _cols;

                for (int c = c0; c <= c1; c++)
                {
                    if (c < 0 || c >= _cols) continue;
                    if (!edgeRow && c != c0 && c != c1) continue;

                    int cell = rowOffset + c;
                    int end = _cellStart[cell + 1];
                    for (int i = _cellStart[cell]; i < end; i++)
                    {
                        int s = _cellItems[i];
                        if (_stamp[s] == generation) continue;
                        _stamp[s] = generation;

                        double distSq = SegmentDistanceSquared(px, py, _ax[s], _ay[s], _bx[s], _by[s]);
                        if (distSq < best) best = distSq;
                    }
                }
            }
        }

        return best == 0 ? 0 : (inside ? 1 : -1) * Math.Sqrt(best);
    }

    /// <summary>
    /// Squared distance from a point to a segment, computed exactly like the linear scan does.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double SegmentDistanceSquared(double px, double py, double ax, double ay, double bx, double by)
    {
        double x = ax, y = ay;
        double dx = bx - x, dy = by - y;

        if (dx != 0 || dy != 0)
        {
            double t = ((px - x) * dx + (py - y) * dy) / (dx * dx + dy * dy);

            if (t > 1)
            {
                x = bx;
                y = by;
            }
            else if (t > 0)
            {
                x += dx * t;
                y += dy * t;
            }
        }

        dx = px - x;
        dy = py - y;

        return dx * dx + dy * dy;
    }

    public void Dispose()
    {
        ArrayPool<double>.Shared.Return(_ax);
        ArrayPool<double>.Shared.Return(_ay);
        ArrayPool<double>.Shared.Return(_bx);
        ArrayPool<double>.Shared.Return(_by);
        ArrayPool<int>.Shared.Return(_cellStart);
        ArrayPool<int>.Shared.Return(_cellItems);
        ArrayPool<int>.Shared.Return(_stamp);
    }
}

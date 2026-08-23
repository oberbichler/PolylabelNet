using System.Runtime.CompilerServices;

namespace Polylabel;

/// <summary>
/// Provides methods for finding the pole of inaccessibility of a polygon.
/// </summary>
/// <remarks>
/// <para>
/// The precision is an absolute length in the polygon's own coordinate units, not a relative
/// tolerance. It should be clearly smaller than the shorter side of the polygon's bounding box;
/// the default of 1.0 suits projected coordinates in metres and is far too coarse for geographic
/// coordinates in degrees, where something like 1e-6 is appropriate.
/// </para>
/// <para>
/// If the precision is at least as large as the shorter bounding box side, the search has nothing
/// to refine and returns the better of the polygon centroid and the bounding box centre. The
/// result is still a point inside the polygon with its true distance to the outline, but it is
/// not refined any further.
/// </para>
/// </remarks>
public static class PoleOfInaccessibility
{
    /// <summary>
    /// Finds the pole of inaccessibility for the given standard polygon with the specified precision.
    /// </summary>
    /// <param name="polygon">The standard polygon coordinates.</param>
    /// <param name="precision">The search precision; must be positive and finite (default is 1.0).</param>
    /// <param name="trace">Optional callback receiving diagnostic lines about the search (default is none).</param>
    /// <returns>A PolylabelResult containing the found pole and its distance to the outline.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is zero, negative, NaN or infinite.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="polygon"/> has a coordinate that is NaN or infinite.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PolylabelResult Find(Polygon polygon, double precision = 1.0, Action<string>? trace = null)
    {
        return Find<Polygon, Point>(polygon, precision, trace);
    }

    /// <summary>
    /// Finds the pole of inaccessibility for the given generic polygon with the specified precision.
    /// Supports any point type implementing the IPoint interface with zero runtime overhead.
    /// </summary>
    /// <typeparam name="TPoint">The type of the point, which must be a struct implementing IPoint.</typeparam>
    /// <param name="polygon">The generic polygon coordinates.</param>
    /// <param name="precision">The search precision; must be positive and finite (default is 1.0).</param>
    /// <param name="trace">Optional callback receiving diagnostic lines about the search (default is none).</param>
    /// <returns>A PolylabelResult containing the found pole and its distance to the outline.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is zero, negative, NaN or infinite.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="polygon"/> has a coordinate that is NaN or infinite.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PolylabelResult Find<TPoint>(Polygon<TPoint> polygon, double precision = 1.0, Action<string>? trace = null)
        where TPoint : struct, IPoint
    {
        return Find<Polygon<TPoint>, TPoint>(polygon, precision, trace);
    }

    /// <summary>
    /// Finds the pole of inaccessibility for any custom polygon implementation with the specified precision.
    /// Supports completely custom third-party polygon types (e.g. NetTopologySuite) with zero runtime overhead.
    /// </summary>
    /// <typeparam name="TPolygon">The type of the polygon, which must be a struct implementing IPolygon&lt;TPoint&gt;.</typeparam>
    /// <typeparam name="TPoint">The type of the point, which must be a struct implementing IPoint.</typeparam>
    /// <param name="polygon">The custom polygon coordinates.</param>
    /// <param name="precision">The search precision; must be positive and finite (default is 1.0).</param>
    /// <param name="trace">Optional callback receiving diagnostic lines about the search (default is none).</param>
    /// <returns>A PolylabelResult containing the found pole and its distance to the outline.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is zero, negative, NaN or infinite.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="polygon"/> has a coordinate that is NaN or infinite.
    /// </exception>
    public static PolylabelResult Find<TPolygon, TPoint>(TPolygon polygon, double precision = 1.0, Action<string>? trace = null)
        where TPolygon : struct, IPolygon<TPoint>
        where TPoint : struct, IPoint
    {
        return FindCore<TPolygon, TPoint, NativeCellQueue>(polygon, new NativeCellQueue(), precision, trace);
    }

    internal static PolylabelResult FindCore<TPolygon, TPoint, TCellQueue>(
        TPolygon polygon, TCellQueue cellQueue, double precision, Action<string>? trace)
        where TPolygon : struct, IPolygon<TPoint>
        where TPoint : struct, IPoint
        where TCellQueue : struct, ICellQueue
    {
        Validate<TPolygon, TPoint>(polygon, precision);

        int ringCount = polygon.RingCount;
        if (ringCount == 0)
        {
            return new PolylabelResult(new Point(0, 0), 0);
        }

        ReadOnlySpan<TPoint> outerRing = polygon.GetRing(0);
        if (outerRing.Length == 0)
        {
            return new PolylabelResult(new Point(0, 0), 0);
        }

        // 1. Find the bounding box of the outer ring
        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;

        for (int i = 0; i < outerRing.Length; i++)
        {
            TPoint p = outerRing[i];
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        double width = maxX - minX;
        double height = maxY - minY;
        double cellSize = Math.Max(precision, Math.Min(width, height));

        // 2. Take centroid as the first best guess
        Cell bestCell = GetCentroidCell<TPolygon, TPoint>(polygon);

        // 3. Second guess: bounding box centroid
        Cell bboxCell = CreateCell<TPolygon, TPoint>(minX + width / 2.0, minY + height / 2.0, 0, polygon);
        if (bboxCell.D > bestCell.D)
        {
            bestCell = bboxCell;
        }

        int numProbes = 2;

        // The requested precision is at least as coarse as the shorter bounding box side, so the
        // quad-tree has nothing left to refine and the two guesses above are already the answer.
        //
        // This deviates from the reference implementation, which returns the bounding box corner
        // with a distance of zero. That corner is composed of the extremes of two different
        // vertices and is therefore regularly outside the polygon - the worst possible answer for
        // label placement, and one that is reported with a fabricated distance of zero. Returning
        // the better of centroid and bounding box centre preserves the invariant that a result is
        // never outside the polygon, and costs nothing that is not computed anyway.
        //
        // Truly degenerate polygons (zero area, single point) are unaffected: GetCentroidCell
        // already falls back to the first vertex there, which yields the same result as before.
        if (cellSize == precision)
        {
            double shorterSide = Math.Min(width, height);
            trace?.Invoke(FormattableString.Invariant(
                $"precision {precision} is not finer than the shorter bounding box side {shorterSide}; returning the best initial guess without refinement"));
            trace?.Invoke(FormattableString.Invariant($"num probes: {numProbes}"));
            trace?.Invoke(FormattableString.Invariant($"best distance: {bestCell.D}"));

            return new PolylabelResult(new Point(bestCell.X, bestCell.Y), bestCell.D);
        }

        // 4. Index the segments, then cover the polygon with initial cells.
        //
        // The index is built only now: the two guesses above are a single pass each, and the
        // coarse precision shortcut above returns without ever running the search, so building
        // an index for them would cost more than it saves. From here the search issues hundreds
        // to thousands of queries and the index pays for itself many times over.
        using SegmentGrid? grid = SegmentGrid.TryBuild<TPolygon, TPoint>(polygon);

        double initialH = cellSize / 2.0;
        for (double x = minX; x < maxX; x += cellSize)
        {
            for (double y = minY; y < maxY; y += cellSize)
            {
                PotentiallyQueue<TPolygon, TPoint, TCellQueue>(x + initialH, y + initialH, initialH, polygon, ref numProbes, ref bestCell, cellQueue, precision, trace, grid);
            }
        }

        // 5. Main queue processing loop
        while (cellQueue.Count > 0)
        {
            Cell cell = cellQueue.Dequeue();

            // Do not drill down further if there's no chance of a better solution
            if (cell.Max - bestCell.D <= precision)
            {
                break;
            }

            // Split the cell into four child cells
            double h = cell.H / 2.0;
            PotentiallyQueue<TPolygon, TPoint, TCellQueue>(cell.X - h, cell.Y - h, h, polygon, ref numProbes, ref bestCell, cellQueue, precision, trace, grid);
            PotentiallyQueue<TPolygon, TPoint, TCellQueue>(cell.X + h, cell.Y - h, h, polygon, ref numProbes, ref bestCell, cellQueue, precision, trace, grid);
            PotentiallyQueue<TPolygon, TPoint, TCellQueue>(cell.X - h, cell.Y + h, h, polygon, ref numProbes, ref bestCell, cellQueue, precision, trace, grid);
            PotentiallyQueue<TPolygon, TPoint, TCellQueue>(cell.X + h, cell.Y + h, h, polygon, ref numProbes, ref bestCell, cellQueue, precision, trace, grid);
        }

        if (trace is not null)
        {
            trace(FormattableString.Invariant($"num probes: {numProbes}"));
            trace(FormattableString.Invariant($"best distance: {bestCell.D}"));
        }

        return new PolylabelResult(new Point(bestCell.X, bestCell.Y), bestCell.D);
    }

    /// <summary>
    /// Rejects inputs the search cannot terminate on, or would silently return a wrong result for.
    /// </summary>
    /// <remarks>
    /// A precision of zero or less makes the termination condition (max - bestCell.D &lt;= precision)
    /// unsatisfiable, so the search never ends and the cell queue grows without bound. A non-finite
    /// coordinate in the outer ring turns the bounding box infinite, which makes the initial grid
    /// loop endless. Non-finite coordinates in a hole do not hang, but they poison the distance
    /// function and yield a plausible looking, wrong pole. NaN needs an explicit check: it fails
    /// every comparison and therefore slips through the bounding box unnoticed.
    ///
    /// Scanning all rings costs about 0.06 % of a typical search (measured: 9 us against 15 ms for
    /// a 5,030 vertex polygon), so validating everything up front is cheaper than being surprised.
    /// </remarks>
    private static void Validate<TPolygon, TPoint>(TPolygon polygon, double precision)
        where TPolygon : struct, IPolygon<TPoint>
        where TPoint : struct, IPoint
    {
        if (!double.IsFinite(precision) || precision <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(precision), precision, "Precision must be a positive, finite number.");
        }

        int ringCount = polygon.RingCount;
        for (int r = 0; r < ringCount; r++)
        {
            ReadOnlySpan<TPoint> ring = polygon.GetRing(r);
            for (int i = 0; i < ring.Length; i++)
            {
                TPoint p = ring[i];
                if (!double.IsFinite(p.X) || !double.IsFinite(p.Y))
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"Polygon has a non-finite coordinate at ring {r}, vertex {i}: ({p.X}, {p.Y})."),
                        nameof(polygon));
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PotentiallyQueue<TPolygon, TPoint, TCellQueue>(
        double x, double y, double h,
        TPolygon polygon,
        ref int numProbes,
        ref Cell bestCell,
        TCellQueue cellQueue,
        double precision,
        Action<string>? trace,
        SegmentGrid? grid)
        where TPolygon : struct, IPolygon<TPoint>
        where TPoint : struct, IPoint
        where TCellQueue : struct, ICellQueue
    {
        Cell cell = CreateCell<TPolygon, TPoint>(x, y, h, polygon, grid);
        numProbes++;
        if (cell.Max > bestCell.D + precision)
        {
            cellQueue.Enqueue(cell);
        }

        if (cell.D > bestCell.D)
        {
            bestCell = cell;
            trace?.Invoke(FormattableString.Invariant(
                $"found best {Math.Round(1e4 * cell.D) / 1e4} after {numProbes} probes"));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Cell CreateCell<TPolygon, TPoint>(double x, double y, double h, TPolygon polygon, SegmentGrid? grid = null)
        where TPolygon : struct, IPolygon<TPoint>
        where TPoint : struct, IPoint
    {
        double d = grid is null
            ? PointToPolygonDist<TPolygon, TPoint>(x, y, polygon)
            : grid.SignedDistance(x, y);
        return new Cell(x, y, h, d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Cell GetCentroidCell<TPolygon, TPoint>(TPolygon polygon)
        where TPolygon : struct, IPolygon<TPoint>
        where TPoint : struct, IPoint
    {
        double area = 0;
        double x = 0;
        double y = 0;
        ReadOnlySpan<TPoint> points = polygon.GetRing(0);
        int len = points.Length;
        if (len == 0) return new Cell(0, 0, 0, 0);

        TPoint b = points[len - 1];
        for (int i = 0; i < len; i++)
        {
            TPoint a = points[i];
            double f = a.X * b.Y - b.X * a.Y;
            x += (a.X + b.X) * f;
            y += (a.Y + b.Y) * f;
            area += f * 3.0;
            b = a;
        }

        if (area == 0)
        {
            TPoint first = points[0];
            return CreateCell<TPolygon, TPoint>(first.X, first.Y, 0, polygon);
        }

        double cx = x / area;
        double cy = y / area;
        Cell centroid = CreateCell<TPolygon, TPoint>(cx, cy, 0, polygon);
        if (centroid.D < 0)
        {
            TPoint first = points[0];
            return CreateCell<TPolygon, TPoint>(first.X, first.Y, 0, polygon);
        }

        return centroid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double PointToPolygonDist<TPolygon, TPoint>(double x, double y, TPolygon polygon)
        where TPolygon : struct, IPolygon<TPoint>
        where TPoint : struct, IPoint
    {
        bool inside = false;
        double minDistSq = double.PositiveInfinity;

        int ringCount = polygon.RingCount;
        for (int r = 0; r < ringCount; r++)
        {
            ReadOnlySpan<TPoint> ring = polygon.GetRing(r);
            int len = ring.Length;
            if (len == 0) continue;

            TPoint b = ring[len - 1];
            for (int i = 0; i < len; i++)
            {
                TPoint a = ring[i];

                if ((a.Y > y) != (b.Y > y) &&
                    (x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X))
                {
                    inside = !inside;
                }

                double distSq = GetSegDistSq(x, y, a, b);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                }

                b = a;
            }
        }

        return minDistSq == 0 ? 0 : (inside ? 1 : -1) * Math.Sqrt(minDistSq);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetSegDistSq<TPoint>(double px, double py, in TPoint a, in TPoint b)
        where TPoint : struct, IPoint
    {
        double x = a.X;
        double y = a.Y;
        double dx = b.X - x;
        double dy = b.Y - y;

        if (dx != 0 || dy != 0)
        {
            double t = ((px - x) * dx + (py - y) * dy) / (dx * dx + dy * dy);

            if (t > 1)
            {
                x = b.X;
                y = b.Y;
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
}

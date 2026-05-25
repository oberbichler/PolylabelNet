using System;
using System.Runtime.CompilerServices;
using Polylabel;

namespace Polylabel.Benchmarks;

public static class PolylabelTinyqueue
{
    public static PolylabelResult Run(Polygon polygon, double precision = 1.0, bool debug = false)
    {
        if (polygon.Rings == null || polygon.Rings.Length == 0)
        {
            return new PolylabelResult(new Point(0, 0), 0);
        }

        ReadOnlySpan<Point> outerRing = polygon.Rings[0];
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
            Point p = outerRing[i];
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        double width = maxX - minX;
        double height = maxY - minY;
        double cellSize = Math.Max(precision, Math.Min(width, height));

        if (cellSize == precision)
        {
            return new PolylabelResult(new Point(minX, minY), 0);
        }

        // 2. Setup custom Tinyqueue instead of native PriorityQueue
        var cellQueue = new Tinyqueue<Cell>(compare: (a, b) => b.Max.CompareTo(a.Max));

        // 3. Take centroid as the first best guess
        Cell bestCell = GetCentroidCell(polygon);

        // 4. Second guess: bounding box centroid
        Cell bboxCell = CreateCell(minX + width / 2.0, minY + height / 2.0, 0, polygon);
        if (bboxCell.D > bestCell.D)
        {
            bestCell = bboxCell;
        }

        int numProbes = 2;

        // 5. Cover polygon with initial cells
        double initialH = cellSize / 2.0;
        for (double x = minX; x < maxX; x += cellSize)
        {
            for (double y = minY; y < maxY; y += cellSize)
            {
                PotentiallyQueue(x + initialH, y + initialH, initialH, polygon, ref numProbes, ref bestCell, cellQueue, precision, debug);
            }
        }

        // 6. Main queue processing loop
        while (cellQueue.Length > 0)
        {
            Cell cell = cellQueue.Pop();

            // Do not drill down further if there's no chance of a better solution
            if (cell.Max - bestCell.D <= precision)
            {
                break;
            }

            // Split the cell into four child cells
            double h = cell.H / 2.0;
            PotentiallyQueue(cell.X - h, cell.Y - h, h, polygon, ref numProbes, ref bestCell, cellQueue, precision, debug);
            PotentiallyQueue(cell.X + h, cell.Y - h, h, polygon, ref numProbes, ref bestCell, cellQueue, precision, debug);
            PotentiallyQueue(cell.X - h, cell.Y + h, h, polygon, ref numProbes, ref bestCell, cellQueue, precision, debug);
            PotentiallyQueue(cell.X + h, cell.Y + h, h, polygon, ref numProbes, ref bestCell, cellQueue, precision, debug);
        }

        if (debug)
        {
            Console.WriteLine($"num probes: {numProbes}\nbest distance: {bestCell.D}");
        }

        return new PolylabelResult(new Point(bestCell.X, bestCell.Y), bestCell.D);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PotentiallyQueue(
        double x, double y, double h,
        Polygon polygon,
        ref int numProbes,
        ref Cell bestCell,
        Tinyqueue<Cell> cellQueue,
        double precision,
        bool debug)
    {
        Cell cell = CreateCell(x, y, h, polygon);
        numProbes++;
        if (cell.Max > bestCell.D + precision)
        {
            cellQueue.Push(cell);
        }

        if (cell.D > bestCell.D)
        {
            bestCell = cell;
            if (debug)
            {
                Console.WriteLine($"found best {Math.Round(1e4 * cell.D) / 1e4} after {numProbes} probes");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Cell CreateCell(double x, double y, double h, Polygon polygon)
    {
        double d = PointToPolygonDist(x, y, polygon);
        return new Cell(x, y, h, d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Cell GetCentroidCell(Polygon polygon)
    {
        double area = 0;
        double x = 0;
        double y = 0;
        ReadOnlySpan<Point> points = polygon.Rings[0];
        int len = points.Length;
        if (len == 0) return new Cell(0, 0, 0, 0);

        Point b = points[len - 1];
        for (int i = 0; i < len; i++)
        {
            Point a = points[i];
            double f = a.X * b.Y - b.X * a.Y;
            x += (a.X + b.X) * f;
            y += (a.Y + b.Y) * f;
            area += f * 3.0;
            b = a;
        }

        if (area == 0)
        {
            Point first = points[0];
            return CreateCell(first.X, first.Y, 0, polygon);
        }

        double cx = x / area;
        double cy = y / area;
        Cell centroid = CreateCell(cx, cy, 0, polygon);
        if (centroid.D < 0)
        {
            Point first = points[0];
            return CreateCell(first.X, first.Y, 0, polygon);
        }

        return centroid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double PointToPolygonDist(double x, double y, Polygon polygon)
    {
        bool inside = false;
        double minDistSq = double.PositiveInfinity;

        for (int r = 0; r < polygon.Rings.Length; r++)
        {
            ReadOnlySpan<Point> ring = polygon.Rings[r];
            int len = ring.Length;
            if (len == 0) continue;

            Point b = ring[len - 1];
            for (int i = 0; i < len; i++)
            {
                Point a = ring[i];

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
    private static double GetSegDistSq(double px, double py, in Point a, in Point b)
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

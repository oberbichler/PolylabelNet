using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Polylabel;

// Same rationale as PolylabelTests: not nested inside the "Polylabel" namespace, so that
// names resolve exactly the way they do for an external consumer.
namespace PolylabelConsumerTests;

/// <summary>
/// Exercises the spatial index that kicks in for larger polygons.
///
/// The index must be invisible: same result as the linear scan, bit for bit. These tests build
/// shapes that sit on both sides of the threshold and that stress the index in the ways it can
/// realistically break - long segments spanning many cells, everything crammed into one cell,
/// extreme aspect ratios, and pooled buffers reused across calls and threads.
/// </summary>
public class SegmentGridTests
{
    /// <summary>Distance from a point to the polygon outline, computed the naive way.</summary>
    private static double ReferenceDistance(Point[][] rings, double px, double py)
    {
        bool inside = false;
        double minDistSq = double.PositiveInfinity;

        foreach (var ring in rings)
        {
            int len = ring.Length;
            if (len == 0) continue;

            Point b = ring[len - 1];
            for (int i = 0; i < len; i++)
            {
                Point a = ring[i];
                if (a.Y > py != b.Y > py && px < (b.X - a.X) * (py - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }

                double x = a.X, y = a.Y, dx = b.X - x, dy = b.Y - y;
                if (dx != 0 || dy != 0)
                {
                    double t = ((px - x) * dx + (py - y) * dy) / (dx * dx + dy * dy);
                    if (t > 1) { x = b.X; y = b.Y; }
                    else if (t > 0) { x += dx * t; y += dy * t; }
                }
                dx = px - x; dy = py - y;
                double distSq = dx * dx + dy * dy;
                if (distSq < minDistSq) minDistSq = distSq;

                b = a;
            }
        }

        return minDistSq == 0 ? 0 : (inside ? 1 : -1) * Math.Sqrt(minDistSq);
    }

    /// <summary>
    /// Compares the pole found by the library against a brute force search over the same grid of
    /// candidate points. The brute force result is a lower bound: whatever the library returns
    /// must be at least as good.
    /// </summary>
    private static void AssertAtLeastAsGoodAsBruteForce(Point[][] rings, double precision, int samples = 60)
    {
        var (point, distance) = PoleOfInaccessibility.Find(new Polygon(rings), precision);

        double minX = rings[0].Min(p => p.X), maxX = rings[0].Max(p => p.X);
        double minY = rings[0].Min(p => p.Y), maxY = rings[0].Max(p => p.Y);

        double best = double.NegativeInfinity;
        for (int i = 0; i <= samples; i++)
        {
            for (int j = 0; j <= samples; j++)
            {
                double x = minX + (maxX - minX) * i / samples;
                double y = minY + (maxY - minY) * j / samples;
                double d = ReferenceDistance(rings, x, y);
                if (d > best) best = d;
            }
        }

        Assert.True(distance >= best - precision,
            $"found {distance} at ({point.X}, {point.Y}), but brute force reached {best}");

        // and the reported distance has to be the truth about the reported point
        Assert.Equal(ReferenceDistance(rings, point.X, point.Y), distance);
    }

    /// <summary>A circle approximated by <paramref name="vertices"/> segments.</summary>
    private static Point[][] Circle(int vertices, double radius = 1000)
    {
        var ring = new Point[vertices + 1];
        for (int i = 0; i < vertices; i++)
        {
            double a = 2 * Math.PI * i / vertices;
            ring[i] = new Point(radius * Math.Cos(a), radius * Math.Sin(a));
        }
        ring[vertices] = ring[0];
        return new[] { ring };
    }

    [Theory]
    [InlineData(64)]     // clearly below the threshold: linear scan
    [InlineData(511)]    // one below
    [InlineData(512)]    // exactly at the threshold: index
    [InlineData(513)]
    [InlineData(4000)]
    public void MatchesTheLinearScanOnBothSidesOfTheThreshold(int vertices)
    {
        var rings = Circle(vertices);

        var (point, distance) = PoleOfInaccessibility.Find(new Polygon(rings), 1.0);

        Assert.Equal(ReferenceDistance(rings, point.X, point.Y), distance);

        // the pole of a regular polygon is its centre, at the inradius
        double inradius = 1000 * Math.Cos(Math.PI / vertices);
        Assert.True(distance >= inradius - 1.0,
            $"expected about {inradius} at the centre, got {distance}");
    }

    [Fact]
    public void HandlesLongSegmentsSpanningManyCells()
    {
        // A star with long spikes: every segment covers a large part of the bounding box, which
        // is what makes a naive grid blow up. The index has to coarsen instead.
        const int spikes = 800;
        var ring = new List<Point>();
        for (int i = 0; i < spikes * 2; i++)
        {
            double a = Math.PI * i / spikes;
            double r = i % 2 == 0 ? 1000 : 40;
            ring.Add(new Point(r * Math.Cos(a), r * Math.Sin(a)));
        }
        ring.Add(ring[0]);
        var rings = new[] { ring.ToArray() };

        AssertAtLeastAsGoodAsBruteForce(rings, 1.0, samples: 40);
    }

    [Fact]
    public void HandlesAnExtremeAspectRatio()
    {
        // Very wide and very flat: the grid ends up as a single row of cells.
        var ring = new List<Point>();
        for (int i = 0; i <= 600; i++) ring.Add(new Point(i * 100.0, 0));
        for (int i = 600; i >= 0; i--) ring.Add(new Point(i * 100.0, 30));
        ring.Add(ring[0]);
        var rings = new[] { ring.ToArray() };

        AssertAtLeastAsGoodAsBruteForce(rings, 0.5, samples: 40);
    }

    [Fact]
    public void HandlesVerticesCrowdedIntoOneSpot()
    {
        // Nearly all vertices collapse onto a tiny arc, so one cell holds almost everything.
        var ring = new List<Point> { new Point(0, 0), new Point(1000, 0), new Point(1000, 1000) };
        for (int i = 0; i < 900; i++)
        {
            double a = Math.PI / 2 + Math.PI / 2 * i / 900.0;
            ring.Add(new Point(500 + 0.5 * Math.Cos(a), 1000 + 0.5 * Math.Sin(a)));
        }
        ring.Add(new Point(0, 1000));
        ring.Add(ring[0]);
        var rings = new[] { ring.ToArray() };

        AssertAtLeastAsGoodAsBruteForce(rings, 1.0, samples: 40);
    }

    [Fact]
    public void HandlesManyHoles()
    {
        var rings = new List<Point[]> { new[]
        {
            new Point(0, 0), new Point(1000, 0), new Point(1000, 1000), new Point(0, 1000), new Point(0, 0)
        } };
        for (int i = 0; i < 400; i++)
        {
            double cx = 25 + i % 20 * 50, cy = 25 + i / 20 * 50;
            rings.Add(new[]
            {
                new Point(cx, cy), new Point(cx + 20, cy),
                new Point(cx + 20, cy + 20), new Point(cx, cy + 20), new Point(cx, cy)
            });
        }

        AssertAtLeastAsGoodAsBruteForce(rings.ToArray(), 1.0, samples: 50);
    }

    [Fact]
    public void RepeatedCallsStayIdentical()
    {
        // The index rents its buffers from a pool. A buffer handed back dirty, or a stale
        // generation stamp, would show up as results drifting between calls.
        var polygon = new Polygon(Circle(2000));
        var expected = PoleOfInaccessibility.Find(polygon, 1.0);

        for (int i = 0; i < 50; i++)
        {
            var actual = PoleOfInaccessibility.Find(polygon, 1.0);

            Assert.Equal(expected.Point.X, actual.Point.X);
            Assert.Equal(expected.Point.Y, actual.Point.Y);
            Assert.Equal(expected.Distance, actual.Distance);
        }
    }

    [Fact]
    public void ConcurrentCallsStayIdentical()
    {
        // Pooled buffers are shared process wide; each search must own its own.
        var polygon = new Polygon(Circle(2000));
        var other = new Polygon(Circle(1500, radius: 250));
        var expected = PoleOfInaccessibility.Find(polygon, 1.0);
        var expectedOther = PoleOfInaccessibility.Find(other, 1.0);

        var results = new PolylabelResult[64];
        Parallel.For(0, 64, i =>
        {
            // interleave two different polygons to provoke buffer mix-ups
            results[i] = PoleOfInaccessibility.Find(i % 2 == 0 ? polygon : other, 1.0);
        });

        for (int i = 0; i < results.Length; i++)
        {
            var want = i % 2 == 0 ? expected : expectedOther;
            Assert.Equal(want.Point.X, results[i].Point.X);
            Assert.Equal(want.Point.Y, results[i].Point.Y);
            Assert.Equal(want.Distance, results[i].Distance);
        }
    }

    [Fact]
    public void WorksWithCustomPolygonTypes()
    {
        // The index reads the rings through IPolygon, so a custom implementation has to work too.
        var rings = Circle(1200);
        var custom = new SpanPolygon(rings[0]);

        var viaCustom = PoleOfInaccessibility.Find<SpanPolygon, Point>(custom, 1.0);
        var viaBuiltIn = PoleOfInaccessibility.Find(new Polygon(rings), 1.0);

        Assert.Equal(viaBuiltIn.Point.X, viaCustom.Point.X);
        Assert.Equal(viaBuiltIn.Point.Y, viaCustom.Point.Y);
        Assert.Equal(viaBuiltIn.Distance, viaCustom.Distance);
    }

    private readonly struct SpanPolygon : IPolygon<Point>
    {
        private readonly Point[] _ring;

        public SpanPolygon(Point[] ring) => _ring = ring;

        public int RingCount => 1;

        public ReadOnlySpan<Point> GetRing(int index) => index == 0 ? _ring : ReadOnlySpan<Point>.Empty;
    }
}

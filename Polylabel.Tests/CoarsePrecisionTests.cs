using System;
using Xunit;
using Polylabel;

// Same rationale as PolylabelTests: not nested inside the "Polylabel" namespace, so that
// names resolve exactly the way they do for an external consumer.
namespace PolylabelConsumerTests;

/// <summary>
/// Covers the case where the requested precision is coarser than the shorter side of the
/// polygon's bounding box, so the quad-tree search has nothing left to refine.
///
/// The reference implementation returns the bounding box corner with a distance of zero
/// here. That corner is built from two different vertices and therefore frequently lies
/// outside the polygon, which is the worst possible answer for label placement. Instead
/// the best of the two initial guesses (centroid and bounding box centre) is returned,
/// which keeps the invariant that a result is never outside the polygon.
/// </summary>
public class CoarsePrecisionTests
{
    private static Polygon Rect(double width, double height) => new Polygon(new[] { new[]
    {
        new Point(0, 0), new Point(width, 0), new Point(width, height), new Point(0, height), new Point(0, 0)
    } });

    [Fact]
    public void ReturnsTheRealPoleWhenPrecisionExceedsTheShorterBoundingBoxSide()
    {
        var (point, distance) = PoleOfInaccessibility.Find(Rect(1, 5), 1.0);

        Assert.Equal(0.5, point.X, precision: 12);
        Assert.Equal(2.5, point.Y, precision: 12);
        Assert.Equal(0.5, distance, precision: 12);
    }

    [Fact]
    public void ReturnsTheRealPoleWhenPrecisionEqualsTheBoundingBoxSide()
    {
        var (point, distance) = PoleOfInaccessibility.Find(Rect(10, 10), 10.0);

        Assert.Equal(5.0, point.X, precision: 12);
        Assert.Equal(5.0, point.Y, precision: 12);
        Assert.Equal(5.0, distance, precision: 12);
    }

    [Fact]
    public void HandlesSliversInsteadOfCollapsingToTheOrigin()
    {
        var (point, distance) = PoleOfInaccessibility.Find(Rect(10, 0.5), 1.0);

        Assert.Equal(5.0, point.X, precision: 12);
        Assert.Equal(0.25, point.Y, precision: 12);
        Assert.Equal(0.25, distance, precision: 12);
    }

    [Fact]
    public void HandlesDegreeCoordinatesWithTheDefaultPrecision()
    {
        // A WGS84 polygon spanning less than a degree: with the default precision of 1.0
        // every such polygon used to collapse to its bounding box corner.
        var polygon = new Polygon(new[] { new[]
        {
            new Point(9.53, 47.06), new Point(9.98, 47.02), new Point(10.23, 47.28),
            new Point(10.19, 47.58), new Point(9.87, 47.55), new Point(9.62, 47.53),
            new Point(9.55, 47.31), new Point(9.53, 47.06)
        } });

        var (point, distance) = PoleOfInaccessibility.Find(polygon);

        Assert.True(distance > 0.2, $"expected a usable distance, got {distance}");
        Assert.InRange(point.X, 9.53, 10.23);
        Assert.InRange(point.Y, 47.02, 47.58);
    }

    [Fact]
    public void NeverReturnsAPointOutsideThePolygon()
    {
        // A diamond: none of its four bounding box corners lies inside the shape. Containment
        // is checked independently here, because the degenerate branch used to report a
        // hard-coded distance of zero for a point that was in fact outside.
        var ring = new[]
        {
            new Point(3, 0), new Point(6, 3), new Point(3, 6), new Point(0, 3), new Point(3, 0)
        };
        var polygon = new Polygon(new[] { ring });

        foreach (double precision in new[] { 0.01, 1.0, 3.0, 6.0, 100.0 })
        {
            var (point, distance) = PoleOfInaccessibility.Find(polygon, precision);

            Assert.True(Contains(ring, point),
                $"precision {precision} returned ({point.X}, {point.Y}), which is outside the polygon (reported d={distance})");
            Assert.True(distance >= 0, $"precision {precision} reported a negative distance {distance}");
        }
    }

    /// <summary>Independent ray casting containment check, used to audit the returned point.</summary>
    private static bool Contains(Point[] ring, Point p)
    {
        bool inside = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            Point a = ring[i], b = ring[j];
            if (a.Y > p.Y != b.Y > p.Y &&
                p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    [Fact]
    public void StillCollapsesForTrulyDegeneratePolygons()
    {
        // Zero area: there is no centroid to fall back to, so the first vertex remains
        // the only sensible answer.
        var collinear = new Polygon(new[] { new[]
        {
            new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(0, 0)
        } });

        var (point, distance) = PoleOfInaccessibility.Find(collinear);

        Assert.Equal(0, point.X);
        Assert.Equal(0, point.Y);
        Assert.Equal(0, distance);
    }
}

using System;
using Xunit;
using Polylabel;

// Same rationale as PolylabelTests: not nested inside the "Polylabel" namespace, so that
// names resolve exactly the way they do for an external consumer.
namespace PolylabelConsumerTests;

/// <summary>
/// Guards the construction of polygons from user data.
///
/// Malformed input used to surface as a bare NullReferenceException or
/// IndexOutOfRangeException from inside the library, or - for null rings - as a silently
/// wrong result. Both are replaced by argument exceptions that name the offending ring
/// and vertex.
/// </summary>
public class PolygonConstructionTests
{
    private static Point[] Square() => new[]
    {
        new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10), new Point(0, 0)
    };

    // --- null rings -----------------------------------------------------------------

    [Fact]
    public void RejectsNullOuterRing()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Polygon(new Point[][] { null! }));

        Assert.Equal("rings", ex.ParamName);
        Assert.Contains("ring 0", ex.Message);
    }

    [Fact]
    public void RejectsNullHoleRing()
    {
        // Used to be ignored silently, dropping the hole from the calculation.
        var ex = Assert.Throws<ArgumentException>(() => new Polygon(new[] { Square(), null! }));

        Assert.Contains("ring 1", ex.Message);
    }

    [Fact]
    public void RejectsNullRingInGenericPolygon()
    {
        Assert.Throws<ArgumentException>(() => new Polygon<Point>(new Point[][] { null! }));
    }

    [Fact]
    public void RejectsNullRingInGeoJsonCoordinates()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Polygon(new double[][][] { null! }));

        Assert.Equal("coordinates", ex.ParamName);
        Assert.Contains("ring 0", ex.Message);
    }

    // --- malformed coordinates ------------------------------------------------------

    [Fact]
    public void RejectsNullCoordinate()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Polygon(new double[][][]
        {
            new[] { new double[] { 0, 0 }, null!, new double[] { 1, 1 } }
        }));

        Assert.Contains("ring 0", ex.Message);
        Assert.Contains("vertex 1", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void RejectsCoordinateWithFewerThanTwoValues(int values)
    {
        var ex = Assert.Throws<ArgumentException>(() => new Polygon(new double[][][]
        {
            new[] { new double[] { 0, 0 }, new double[values], new double[] { 1, 1 } }
        }));

        Assert.Contains("vertex 1", ex.Message);
    }

    [Fact]
    public void AcceptsCoordinatesWithElevation()
    {
        // GeoJSON positions may carry a third value; it is ignored.
        var polygon = new Polygon(new double[][][]
        {
            new[]
            {
                new double[] { 0, 0, 100 }, new double[] { 10, 0, 100 },
                new double[] { 10, 10, 100 }, new double[] { 0, 10, 100 }, new double[] { 0, 0, 100 }
            }
        });

        var (point, distance) = PoleOfInaccessibility.Find(polygon, 1.0);

        Assert.Equal(5.0, point.X);
        Assert.Equal(5.0, point.Y);
        Assert.Equal(5.0, distance);
    }

    // --- unchanged behaviour ---------------------------------------------------------

    [Fact]
    public void StillRejectsNullContainers()
    {
        Assert.Throws<ArgumentNullException>(() => new Polygon((Point[][])null!));
        Assert.Throws<ArgumentNullException>(() => new Polygon((double[][][])null!));
        Assert.Throws<ArgumentNullException>(() => new Polygon<Point>(null!));
    }

    [Fact]
    public void StillAcceptsEmptyRingsAndEmptyPolygons()
    {
        // Empty is a valid degenerate value, unlike null: no exception, result (0, 0).
        Assert.Equal(0, PoleOfInaccessibility.Find(new Polygon(Array.Empty<Point[]>())).Distance);
        Assert.Equal(0, PoleOfInaccessibility.Find(new Polygon(new[] { Array.Empty<Point>() })).Distance);
        Assert.Equal(0, PoleOfInaccessibility.Find(new Polygon(new double[][][] { Array.Empty<double[]>() })).Distance);

        var withEmptyHole = new Polygon(new[] { Square(), Array.Empty<Point>() });
        Assert.Equal(5.0, PoleOfInaccessibility.Find(withEmptyHole, 1.0).Distance);
    }
}

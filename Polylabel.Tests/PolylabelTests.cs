using System;
using System.IO;
using System.Text.Json;
using System.Globalization;
using Xunit;
using Polylabel;

// Deliberately NOT nested inside the "Polylabel" namespace: these tests must resolve
// names exactly like an external consumer does, so that a namespace/type collision on
// the public entry point fails the build here instead of only at the consumer's site.
namespace PolylabelConsumerTests;

public class PolylabelTests
{
    private static Polygon LoadFixture(string filename)
    {
        string fullPath = Path.Combine(AppContext.BaseDirectory, "fixtures", filename);
        string json = File.ReadAllText(fullPath);
        double[][][] coords = JsonSerializer.Deserialize<double[][][]>(json)
            ?? throw new Exception($"Failed to deserialize {filename}");
        return new Polygon(coords);
    }

    [Fact]
    public void FindsPoleOfInaccessibilityForWater1AndPrecision1()
    {
        var water1 = LoadFixture("water1.json");
        var (point, distance) = PoleOfInaccessibility.Find(water1, 1.0);

        Assert.Equal(3865.85009765625, point.X);
        Assert.Equal(2124.87841796875, point.Y);
        Assert.Equal(288.8493574779127, distance, precision: 12);
    }

    [Fact]
    public void FindsPoleOfInaccessibilityForWater1AndPrecision50()
    {
        var water1 = LoadFixture("water1.json");
        var (point, distance) = PoleOfInaccessibility.Find(water1, 50.0);

        Assert.Equal(3854.296875, point.X);
        Assert.Equal(2123.828125, point.Y);
        Assert.Equal(278.5795872381558, distance, precision: 12);
    }

    [Fact]
    public void FindsPoleOfInaccessibilityForWater2AndDefaultPrecision1()
    {
        var water2 = LoadFixture("water2.json");
        var (point, distance) = PoleOfInaccessibility.Find(water2, 1.0);

        Assert.Equal(3263.5, point.X);
        Assert.Equal(3263.5, point.Y);
        Assert.Equal(960.5, distance, precision: 12);
    }

    [Fact]
    public void WorksOnDegeneratePolygons()
    {
        var p1Coords = new double[][][] { new double[][] { new double[] { 0, 0 }, new double[] { 1, 0 }, new double[] { 2, 0 }, new double[] { 0, 0 } } };
        var polygon1 = new Polygon(p1Coords);
        var (point1, distance1) = PoleOfInaccessibility.Find(polygon1);

        Assert.Equal(0, point1.X);
        Assert.Equal(0, point1.Y);
        Assert.Equal(0, distance1);

        var p2Coords = new double[][][] { new double[][] { new double[] { 0, 0 }, new double[] { 1, 0 }, new double[] { 1, 1 }, new double[] { 1, 0 }, new double[] { 0, 0 } } };
        var polygon2 = new Polygon(p2Coords);
        var (point2, distance2) = PoleOfInaccessibility.Find(polygon2);

        Assert.Equal(0, point2.X);
        Assert.Equal(0, point2.Y);
        Assert.Equal(0, distance2);
    }

    [Fact]
    public void ReturnsZeroForDefaultPolygonStruct()
    {
        Polygon polygon = default;
        var (point, distance) = PoleOfInaccessibility.Find(polygon);

        Assert.Equal(0, point.X);
        Assert.Equal(0, point.Y);
        Assert.Equal(0, distance);
    }

    [Fact]
    public void ReturnsZeroForDefaultGenericPolygonStruct()
    {
        Polygon<Point> polygon = default;
        var (point, distance) = PoleOfInaccessibility.Find(polygon);

        Assert.Equal(0, point.X);
        Assert.Equal(0, point.Y);
        Assert.Equal(0, distance);
    }

    [Fact]
    public void ReturnsZeroForEmptyPolygon()
    {
        var polygon = new Polygon(Array.Empty<Point[]>());
        var (point, distance) = PoleOfInaccessibility.Find(polygon);

        Assert.Equal(0, point.X);
        Assert.Equal(0, point.Y);
        Assert.Equal(0, distance);
    }

    [Fact]
    public void ReturnsZeroForPolygonWithEmptyOuterRing()
    {
        var polygon = new Polygon(new Point[][] { Array.Empty<Point>() });
        var (point, distance) = PoleOfInaccessibility.Find(polygon);

        Assert.Equal(0, point.X);
        Assert.Equal(0, point.Y);
        Assert.Equal(0, distance);
    }

    [Fact]
    public void ReturnsZeroForSinglePointPolygon()
    {
        var coords = new double[][][] { new double[][] { new double[] { 5, 7 } } };
        var polygon = new Polygon(coords);
        var (point, distance) = PoleOfInaccessibility.Find(polygon);

        Assert.Equal(5, point.X);
        Assert.Equal(7, point.Y);
        Assert.Equal(0, distance);
    }

    private readonly struct CustomVector2 : IPoint
    {
        public double X => XCoord;
        public double Y => YCoord;

        public double XCoord { get; }
        public double YCoord { get; }

        public CustomVector2(double x, double y)
        {
            XCoord = x;
            YCoord = y;
        }
    }

    [Fact]
    public void WorksWithCustomPointType()
    {
        var rings = new CustomVector2[][]
        {
            new CustomVector2[]
            {
                new CustomVector2(0, 0),
                new CustomVector2(10, 0),
                new CustomVector2(10, 10),
                new CustomVector2(0, 10),
                new CustomVector2(0, 0)
            }
        };

        var polygon = new Polygon<CustomVector2>(rings);
        var (point, distance) = PoleOfInaccessibility.Find(polygon, 1.0);

        Assert.Equal(5.0, point.X);
        Assert.Equal(5.0, point.Y);
        Assert.Equal(5.0, distance);
    }

    private readonly struct Vector2Adapter : IPoint
    {
        private readonly System.Numerics.Vector2 _vector;

        public double X => _vector.X;
        public double Y => _vector.Y;

        public Vector2Adapter(System.Numerics.Vector2 vector) => _vector = vector;
    }

    [Fact]
    public void WorksWithExternalVector2()
    {
        var rings = new System.Numerics.Vector2[][]
        {
            new System.Numerics.Vector2[]
            {
                new System.Numerics.Vector2(0, 0),
                new System.Numerics.Vector2(10, 0),
                new System.Numerics.Vector2(10, 10),
                new System.Numerics.Vector2(0, 10),
                new System.Numerics.Vector2(0, 0)
            }
        };

        var wrappedRings = Array.ConvertAll(rings,
            ring => Array.ConvertAll(ring, v => new Vector2Adapter(v)));

        var polygon = new Polygon<Vector2Adapter>(wrappedRings);
        var (point, distance) = PoleOfInaccessibility.Find(polygon, 1.0);

        Assert.Equal(5.0, point.X);
        Assert.Equal(5.0, point.Y);
        Assert.Equal(5.0, distance);
    }

    private readonly struct CustomPolygon : IPolygon<Point>
    {
        private readonly Point[] _outerRing;

        public int RingCount => 1;

        public ReadOnlySpan<Point> GetRing(int index) => index == 0 ? _outerRing : ReadOnlySpan<Point>.Empty;

        public CustomPolygon(Point[] outerRing) => _outerRing = outerRing;
    }

    [Fact]
    public void WorksWithCustomPolygonType()
    {
        var outerRing = new Point[]
        {
            new Point(0, 0),
            new Point(10, 0),
            new Point(10, 10),
            new Point(0, 10),
            new Point(0, 0)
        };

        var polygon = new CustomPolygon(outerRing);
        var (point, distance) = PoleOfInaccessibility.Find<CustomPolygon, Point>(polygon, 1.0);

        Assert.Equal(5.0, point.X);
        Assert.Equal(5.0, point.Y);
        Assert.Equal(5.0, distance);
    }
}

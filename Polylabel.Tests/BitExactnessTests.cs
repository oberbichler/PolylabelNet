using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Xunit;
using Polylabel;

// Same rationale as PolylabelTests: not nested inside the "Polylabel" namespace, so that
// names resolve exactly the way they do for an external consumer.
namespace PolylabelConsumerTests;

/// <summary>
/// Characterization tests pinning the exact output of the search, down to the last bit.
///
/// The distance function is performance sensitive and invites optimisation. Any such change
/// has to be a pure speed-up: these values were recorded from the unoptimised implementation
/// and must not move. Assert.Equal on doubles compares exactly, without tolerance, which is
/// the point - a shortcut that shifts the 13th digit would be caught here.
/// </summary>
public class BitExactnessTests
{
    public static IEnumerable<object[]> Fixtures()
    {
        yield return new object[] { "water1", 100.0, 3887.90625, 2090.21875, 247.8806298213416 };
        yield return new object[] { "water1", 50.0, 3854.296875, 2123.828125, 278.5795872381558 };
        yield return new object[] { "water1", 10.0, 3866.900390625, 2128.029296875, 286.7438478815549 };
        yield return new object[] { "water1", 1.0, 3865.85009765625, 2124.87841796875, 288.8493574779127 };
        yield return new object[] { "water1", 0.5, 3865.85009765625, 2124.87841796875, 288.8493574779127 };
        yield return new object[] { "water1", 0.1, 3865.9813842773438, 2125.0097045898438, 288.92200716105754 };
        yield return new object[] { "water2", 100.0, 3272.0, 3272.0, 952.0 };
        yield return new object[] { "water2", 50.0, 3272.0, 3272.0, 952.0 };
        yield return new object[] { "water2", 10.0, 3263.5, 3263.5, 960.5 };
        yield return new object[] { "water2", 1.0, 3263.5, 3263.5, 960.5 };
        yield return new object[] { "water2", 0.5, 3263.5, 3263.5, 960.5 };
        yield return new object[] { "water2", 0.1, 3263.43359375, 3263.43359375, 960.566085249776 };
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ProducesTheExactSameResultAsBefore(
        string fixture, double precision, double expectedX, double expectedY, double expectedDistance)
    {
        var polygon = LoadFixture(fixture);

        var (point, distance) = PoleOfInaccessibility.Find(polygon, precision);

        Assert.Equal(expectedX, point.X);
        Assert.Equal(expectedY, point.Y);
        Assert.Equal(expectedDistance, distance);
    }

    [Fact]
    public void ProducesTheExactSameResultForShapesWithTrickyDistances()
    {
        // Shapes where the nearest edge changes often while the search descends: a thin
        // diagonal corridor, a comb, and a ring of holes around the pole.
        AssertExact(Corridor(), 0.05, 25.0, 26.0, 1.4427746420619039);
        AssertExact(Comb(), 0.05, 47.3876953125, 10.1318359375, 10.1318359375);
        AssertExact(SurroundedByHoles(), 0.05, 50.0, 50.0, 23.24203904274899);
    }

    private static void AssertExact(Polygon polygon, double precision, double x, double y, double distance)
    {
        var (point, actual) = PoleOfInaccessibility.Find(polygon, precision);

        Assert.Equal(x, point.X);
        Assert.Equal(y, point.Y);
        Assert.Equal(distance, actual);
    }

    private static Polygon Corridor() => new Polygon(new[] { new[]
    {
        new Point(0, 0), new Point(50, 48), new Point(50, 52), new Point(0, 4), new Point(0, 0)
    } });

    private static Polygon Comb()
    {
        var ring = new List<Point> { new Point(0, 0), new Point(100, 0), new Point(100, 100) };
        for (int i = 9; i >= 0; i--)
        {
            ring.Add(new Point(i * 10 + 5, 100));
            ring.Add(new Point(i * 10 + 5, 20));
            ring.Add(new Point(i * 10, 20));
            ring.Add(new Point(i * 10, 100));
        }
        ring.Add(new Point(0, 0));
        return new Polygon(new[] { ring.ToArray() });
    }

    private static Polygon SurroundedByHoles()
    {
        var rings = new List<Point[]> { new[]
        {
            new Point(0, 0), new Point(100, 0), new Point(100, 100), new Point(0, 100), new Point(0, 0)
        } };
        for (int i = 0; i < 12; i++)
        {
            double a = 2 * Math.PI * i / 12;
            double cx = 50 + 30 * Math.Cos(a), cy = 50 + 30 * Math.Sin(a);
            rings.Add(new[]
            {
                new Point(cx - 5, cy - 5), new Point(cx + 5, cy - 5),
                new Point(cx + 5, cy + 5), new Point(cx - 5, cy + 5), new Point(cx - 5, cy - 5)
            });
        }
        return new Polygon(rings.ToArray());
    }

    private static Polygon LoadFixture(string filename)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", filename + ".json");
        double[][][] coords = JsonSerializer.Deserialize<double[][][]>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"failed to read {filename}");
        return new Polygon(coords);
    }
}

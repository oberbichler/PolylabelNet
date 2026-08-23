using System;
using System.Collections.Generic;
using Xunit;
using Polylabel;

// Same rationale as PolylabelTests: not nested inside the "Polylabel" namespace, so that
// names resolve exactly the way they do for an external consumer.
namespace PolylabelConsumerTests;

/// <summary>
/// Covers the optional diagnostic channel. It used to be a bool that made the library write
/// to the console, which a library has no business doing; the caller now decides where the
/// text goes, or gets none at all.
/// </summary>
public class TraceTests
{
    private static Polygon Square() => new Polygon(new[] { new[]
    {
        new Point(0, 0), new Point(100, 0), new Point(100, 100), new Point(0, 100), new Point(0, 0)
    } });

    [Fact]
    public void ReportsProbeCountAndBestDistance()
    {
        var lines = new List<string>();

        PoleOfInaccessibility.Find(Square(), 1.0, lines.Add);

        Assert.NotEmpty(lines);
        Assert.Contains(lines, l => l.Contains("probes"));
        Assert.Contains(lines, l => l.Contains("best distance"));
    }

    [Fact]
    public void ReportsEveryImprovementWhileRefining()
    {
        var lines = new List<string>();

        // An L shape: the centroid is a poor guess, so the search keeps improving on it.
        var lShape = new Polygon(new[] { new[]
        {
            new Point(0, 0), new Point(60, 0), new Point(60, 20), new Point(20, 20),
            new Point(20, 60), new Point(0, 60), new Point(0, 0)
        } });

        PoleOfInaccessibility.Find(lShape, 1.0, lines.Add);

        Assert.Contains(lines, l => l.StartsWith("found best "));
    }

    [Fact]
    public void ReportsNoImprovementWhenTheCentroidIsAlreadyOptimal()
    {
        var lines = new List<string>();

        // For a square the centroid is exactly the pole, so no probe ever beats the
        // initial guess and only the summary is reported.
        PoleOfInaccessibility.Find(Square(), 1.0, lines.Add);

        Assert.DoesNotContain(lines, l => l.StartsWith("found best "));
        Assert.Equal(new[] { "num probes: 71", "best distance: 50" }, lines);
    }

    [Fact]
    public void ExplainsWhyACoarsePrecisionWasNotRefined()
    {
        var lines = new List<string>();

        PoleOfInaccessibility.Find(Square(), 1000.0, lines.Add);

        Assert.Contains(lines, l => l.Contains("not finer than the shorter bounding box side"));
        Assert.Contains(lines, l => l == "num probes: 2");
    }

    [Fact]
    public void TracingDoesNotChangeTheResult()
    {
        var quiet = PoleOfInaccessibility.Find(Square(), 1.0);
        var traced = PoleOfInaccessibility.Find(Square(), 1.0, _ => { });

        Assert.Equal(quiet.Point.X, traced.Point.X);
        Assert.Equal(quiet.Point.Y, traced.Point.Y);
        Assert.Equal(quiet.Distance, traced.Distance);
    }

    [Fact]
    public void TraceIsOptional()
    {
        // No callback, no output, no exception - including for the coarse precision shortcut.
        PoleOfInaccessibility.Find(Square());
        PoleOfInaccessibility.Find(Square(), 1000.0);
        PoleOfInaccessibility.Find(Square(), 1000.0, null);
    }

    [Fact]
    public void TracesAreAvailableOnAllOverloads()
    {
        var lines = new List<string>();

        PoleOfInaccessibility.Find(Square(), 1.0, lines.Add);
        int afterStandard = lines.Count;

        PoleOfInaccessibility.Find(new Polygon<Point>(Square().Rings), 1.0, lines.Add);
        int afterGeneric = lines.Count;

        PoleOfInaccessibility.Find<Polygon, Point>(Square(), 1.0, lines.Add);

        Assert.True(afterStandard > 0);
        Assert.True(afterGeneric > afterStandard);
        Assert.True(lines.Count > afterGeneric);
    }
}

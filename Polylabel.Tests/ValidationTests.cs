using System;
using System.Threading.Tasks;
using Xunit;
using Polylabel;

// Same rationale as PolylabelTests: not nested inside the "Polylabel" namespace, so that
// names resolve exactly the way they do for an external consumer.
namespace PolylabelConsumerTests;

/// <summary>
/// Guards the argument validation of the public entry point.
///
/// Every input in here used to either hang forever while growing the cell queue without
/// bound, or return a plausible looking but wrong result. The tests run on a worker task
/// with a timeout on purpose: should the validation ever be removed, they must fail the
/// build instead of hanging CI. (xUnit honours Timeout for async tests only.)
/// </summary>
public class ValidationTests
{
    private const int HangGuardMs = 15_000;

    private static Task<PolylabelResult> FindAsync(Polygon polygon, double precision) =>
        Task.Run(() => PoleOfInaccessibility.Find(polygon, precision));

    private static Point[] Square(double size = 10) => new[]
    {
        new Point(0, 0), new Point(size, 0), new Point(size, size), new Point(0, size), new Point(0, 0)
    };

    private static Polygon SquarePolygon() => new Polygon(new[] { Square() });

    [Theory(Timeout = HangGuardMs)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(-0.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task RejectsPrecisionThatIsNotPositiveAndFinite(double precision)
    {
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => FindAsync(SquarePolygon(), precision));

        Assert.Equal("precision", ex.ParamName);
    }

    [Theory(Timeout = HangGuardMs)]
    [InlineData(1e-9)]
    [InlineData(0.001)]
    [InlineData(1.0)]
    [InlineData(double.Epsilon)]
    public async Task AcceptsAnyPositiveFinitePrecision(double precision)
    {
        var (point, distance) = await FindAsync(SquarePolygon(), precision);

        Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y));
        Assert.True(distance >= 0);
    }

    [Fact(Timeout = HangGuardMs)]
    public async Task ValidatesPrecisionBeforeInspectingThePolygon()
    {
        // An empty polygon short-circuits to (0, 0); the argument check must still win,
        // otherwise a caller never learns that the precision was nonsense.
        Polygon empty = default;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => FindAsync(empty, -1.0));
    }

    [Theory(Timeout = HangGuardMs)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task RejectsNonFiniteXInOuterRing(double x)
    {
        var polygon = new Polygon(new[] { new[]
        {
            new Point(0, 0), new Point(x, 0), new Point(10, 10), new Point(0, 10), new Point(0, 0)
        } });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => FindAsync(polygon, 1.0));

        Assert.Equal("polygon", ex.ParamName);
    }

    [Theory(Timeout = HangGuardMs)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task RejectsNonFiniteYInOuterRing(double y)
    {
        var polygon = new Polygon(new[] { new[]
        {
            new Point(0, 0), new Point(10, y), new Point(10, 10), new Point(0, 10), new Point(0, 0)
        } });

        await Assert.ThrowsAsync<ArgumentException>(() => FindAsync(polygon, 1.0));
    }

    [Theory(Timeout = HangGuardMs)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task RejectsNonFiniteCoordinateInHole(double x)
    {
        // Holes never enter the bounding box, so these inputs used to produce a plausible
        // looking result instead of failing.
        var polygon = new Polygon(new[]
        {
            Square(),
            new[] { new Point(2, 2), new Point(x, 2), new Point(8, 8), new Point(2, 8), new Point(2, 2) }
        });

        await Assert.ThrowsAsync<ArgumentException>(() => FindAsync(polygon, 1.0));
    }

    [Fact(Timeout = HangGuardMs)]
    public async Task ReportsRingAndVertexIndexOfTheOffendingCoordinate()
    {
        var polygon = new Polygon(new[]
        {
            Square(),
            new[] { new Point(2, 2), new Point(8, 2), new Point(double.NaN, 8), new Point(2, 8), new Point(2, 2) }
        });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => FindAsync(polygon, 1.0));

        Assert.Contains("ring 1", ex.Message);
        Assert.Contains("vertex 2", ex.Message);
    }

    [Fact(Timeout = HangGuardMs)]
    public async Task GenericPolygonOverloadIsValidatedToo()
    {
        var polygon = new Polygon<Point>(new[] { new[]
        {
            new Point(0, 0), new Point(double.PositiveInfinity, 0), new Point(10, 10), new Point(0, 0)
        } });

        await Assert.ThrowsAsync<ArgumentException>(
            () => Task.Run(() => PoleOfInaccessibility.Find(polygon, 1.0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Task.Run(() => PoleOfInaccessibility.Find(polygon, -1.0)));
    }

    [Fact(Timeout = HangGuardMs)]
    public async Task CustomPolygonOverloadIsValidatedToo()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => Task.Run(() => PoleOfInaccessibility.Find<NonFinitePolygon, Point>(new NonFinitePolygon(), 1.0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Task.Run(() => PoleOfInaccessibility.Find<NonFinitePolygon, Point>(new NonFinitePolygon(), 0.0)));
    }

    private readonly struct NonFinitePolygon : IPolygon<Point>
    {
        public int RingCount => 1;

        public ReadOnlySpan<Point> GetRing(int index) => new[]
        {
            new Point(0, 0), new Point(10, 0), new Point(double.NaN, 10), new Point(0, 0)
        };
    }
}

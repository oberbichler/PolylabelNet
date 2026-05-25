using System;

namespace Polylabel;

/// <summary>
/// Represents a generic polygon, composed of one or more linear rings of custom point types.
/// The first ring represents the outer boundary, and subsequent rings represent holes.
/// </summary>
/// <typeparam name="TPoint">The type of point, which must implement IPoint.</typeparam>
public readonly struct Polygon<TPoint> : IPolygon<TPoint> where TPoint : struct, IPoint
{
    public TPoint[][] Rings { get; }

    public int RingCount => Rings.Length;

    public ReadOnlySpan<TPoint> GetRing(int index) => Rings[index];

    public Polygon(TPoint[][] rings) => Rings = rings ?? throw new ArgumentNullException(nameof(rings));
}

/// <summary>
/// Represents a standard polygon, composed of one or more rings of the standard Point type.
/// </summary>
public readonly struct Polygon : IPolygon<Point>
{
    public Point[][] Rings { get; }

    public int RingCount => Rings.Length;

    public ReadOnlySpan<Point> GetRing(int index) => Rings[index];

    public Polygon(Point[][] rings) => Rings = rings ?? throw new ArgumentNullException(nameof(rings));

    public Polygon(double[][][] coordinates)
    {
        if (coordinates == null) throw new ArgumentNullException(nameof(coordinates));

        Rings = new Point[coordinates.Length][];
        for (int i = 0; i < coordinates.Length; i++)
        {
            var ringCoords = coordinates[i];
            var ring = new Point[ringCoords.Length];
            for (int j = 0; j < ringCoords.Length; j++)
            {
                ring[j] = new Point(ringCoords[j][0], ringCoords[j][1]);
            }
            Rings[i] = ring;
        }
    }

    /// <summary>
    /// Implicitly converts a non-generic Polygon to a generic Polygon&lt;Point&gt;.
    /// </summary>
    public static implicit operator Polygon<Point>(Polygon p) => new Polygon<Point>(p.Rings);
}

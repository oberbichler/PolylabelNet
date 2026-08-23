namespace Polylabel;

/// <summary>
/// Represents a generic polygon, composed of one or more linear rings of custom point types.
/// The first ring represents the outer boundary, and subsequent rings represent holes.
/// </summary>
/// <typeparam name="TPoint">The type of point, which must implement IPoint.</typeparam>
public readonly struct Polygon<TPoint> : IPolygon<TPoint> where TPoint : struct, IPoint
{
    /// <summary>The rings of the polygon (outer boundary at index 0, followed by holes).</summary>
    public TPoint[][] Rings { get; }

    /// <inheritdoc />
    public int RingCount => Rings?.Length ?? 0;

    /// <inheritdoc />
    public ReadOnlySpan<TPoint> GetRing(int index) => Rings[index];

    /// <summary>Creates a new polygon from the specified rings.</summary>
    /// <param name="rings">The rings; an empty ring is allowed, a null ring is not.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rings"/> is null.</exception>
    /// <exception cref="ArgumentException">One of the rings is null.</exception>
    public Polygon(TPoint[][] rings)
    {
        Rings = rings ?? throw new ArgumentNullException(nameof(rings));
        PolygonValidation.RequireNoNullRings(rings, nameof(rings));
    }
}

/// <summary>
/// Represents a standard polygon, composed of one or more rings of the standard Point type.
/// </summary>
public readonly struct Polygon : IPolygon<Point>
{
    /// <summary>The rings of the polygon (outer boundary at index 0, followed by holes).</summary>
    public Point[][] Rings { get; }

    /// <inheritdoc />
    public int RingCount => Rings?.Length ?? 0;

    /// <inheritdoc />
    public ReadOnlySpan<Point> GetRing(int index) => Rings[index];

    /// <summary>Creates a new polygon from the specified rings.</summary>
    /// <param name="rings">The rings; an empty ring is allowed, a null ring is not.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rings"/> is null.</exception>
    /// <exception cref="ArgumentException">One of the rings is null.</exception>
    public Polygon(Point[][] rings)
    {
        Rings = rings ?? throw new ArgumentNullException(nameof(rings));
        PolygonValidation.RequireNoNullRings(rings, nameof(rings));
    }

    /// <summary>Creates a new polygon from GeoJSON-style double coordinates.</summary>
    /// <param name="coordinates">
    /// Rings of positions. Each position needs at least two values; any further value
    /// (a GeoJSON elevation, for example) is ignored.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="coordinates"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// A ring or a position is null, or a position has fewer than two values.
    /// </exception>
    public Polygon(double[][][] coordinates)
    {
        if (coordinates is null) throw new ArgumentNullException(nameof(coordinates));

        Rings = new Point[coordinates.Length][];
        for (int i = 0; i < coordinates.Length; i++)
        {
            double[][] ringCoords = coordinates[i]
                ?? throw new ArgumentException($"Polygon ring {i} is null.", nameof(coordinates));

            var ring = new Point[ringCoords.Length];
            for (int j = 0; j < ringCoords.Length; j++)
            {
                double[] position = ringCoords[j]
                    ?? throw new ArgumentException(
                        $"Polygon ring {i}, vertex {j} is null.", nameof(coordinates));

                if (position.Length < 2)
                {
                    throw new ArgumentException(
                        $"Polygon ring {i}, vertex {j} has {position.Length} coordinate values, expected at least 2.",
                        nameof(coordinates));
                }

                ring[j] = new Point(position[0], position[1]);
            }
            Rings[i] = ring;
        }
    }

    /// <summary>
    /// Implicitly converts a non-generic Polygon to a generic Polygon&lt;Point&gt;.
    /// </summary>
    public static implicit operator Polygon<Point>(Polygon p) => new Polygon<Point>(p.Rings);
}

/// <summary>
/// Shared input checks for the built-in polygon types.
/// </summary>
/// <remarks>
/// A null ring cannot be detected further down: the rings are handed to the search as
/// <see cref="ReadOnlySpan{T}"/>, and a null array is indistinguishable from an empty one there.
/// It used to be swallowed silently, which turned a null outer ring into the result (0, 0) and
/// dropped a null hole from the calculation without a word. Empty rings stay valid on purpose -
/// null signals a bug, empty is a legitimate degenerate value.
/// </remarks>
internal static class PolygonValidation
{
    internal static void RequireNoNullRings<T>(T[][] rings, string paramName)
    {
        for (int i = 0; i < rings.Length; i++)
        {
            if (rings[i] is null)
            {
                throw new ArgumentException($"Polygon ring {i} is null.", paramName);
            }
        }
    }
}

namespace Polylabel;

/// <summary>
/// Represents a polygon, composed of one or more rings.
/// The first ring represents the outer boundary, and subsequent rings represent holes.
/// </summary>
public readonly struct Polygon
{
    public Point[][] Rings { get; }

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
}

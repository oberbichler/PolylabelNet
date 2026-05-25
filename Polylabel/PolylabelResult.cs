namespace Polylabel;

/// <summary>
/// Holds the result of a polylabel calculation.
/// </summary>
public readonly struct PolylabelResult
{
    /// <summary>The pole of inaccessibility point.</summary>
    public Point Point { get; }

    /// <summary>The distance from the pole to the nearest polygon edge.</summary>
    public double Distance { get; }

    /// <summary>Creates a new result with the specified point and distance.</summary>
    public PolylabelResult(Point point, double distance)
    {
        Point = point;
        Distance = distance;
    }

    /// <summary>Deconstructs the result into a point and distance.</summary>
    public void Deconstruct(out Point point, out double distance)
    {
        point = Point;
        distance = Distance;
    }
}

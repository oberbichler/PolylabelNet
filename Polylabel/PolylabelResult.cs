namespace Polylabel;

/// <summary>
/// Holds the result of a polylabel calculation.
/// </summary>
public readonly struct PolylabelResult
{
    public Point Point { get; }
    public double Distance { get; }

    public PolylabelResult(Point point, double distance)
    {
        Point = point;
        Distance = distance;
    }

    public void Deconstruct(out Point point, out double distance)
    {
        point = Point;
        distance = Distance;
    }
}

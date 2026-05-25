namespace Polylabel;

/// <summary>
/// Represents a 2D point with double-precision coordinates.
/// </summary>
public readonly struct Point : IPoint
{
    public double X { get; }
    public double Y { get; }

    public Point(double x, double y) => (X, Y) = (x, y);

    public override string ToString() => $"({X}, {Y})";
}

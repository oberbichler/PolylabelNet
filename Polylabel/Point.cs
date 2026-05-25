namespace Polylabel;

/// <summary>
/// Represents a 2D point with double-precision coordinates.
/// </summary>
public readonly struct Point : IPoint
{
    /// <summary>The X coordinate.</summary>
    public double X { get; }

    /// <summary>The Y coordinate.</summary>
    public double Y { get; }

    /// <summary>Creates a new point with the specified coordinates.</summary>
    public Point(double x, double y) => (X, Y) = (x, y);

    /// <inheritdoc />
    public override string ToString() => $"({X}, {Y})";
}

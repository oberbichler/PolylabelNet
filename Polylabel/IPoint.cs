namespace Polylabel;

/// <summary>
/// Defines a contract for a 2D point, enabling zero-overhead generic execution in Polylabel.
/// </summary>
public interface IPoint
{
    /// <summary>The X coordinate.</summary>
    double X { get; }

    /// <summary>The Y coordinate.</summary>
    double Y { get; }
}

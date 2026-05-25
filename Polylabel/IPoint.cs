namespace Polylabel;

/// <summary>
/// Defines a contract for a 2D point, enabling zero-overhead generic execution in Polylabel.
/// </summary>
public interface IPoint
{
    double X { get; }
    double Y { get; }
}

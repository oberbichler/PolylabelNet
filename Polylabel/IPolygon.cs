using System;

namespace Polylabel;

/// <summary>
/// Defines a contract for a polygon, enabling zero-overhead generic execution over custom geometry containers.
/// Rings are accessed as ReadOnlySpans to prevent array copying and heap allocations.
/// </summary>
/// <typeparam name="TPoint">The type of points, which must implement IPoint.</typeparam>
public interface IPolygon<TPoint> where TPoint : struct, IPoint
{
    /// <summary>
    /// Gets the number of rings in the polygon (including holes).
    /// </summary>
    int RingCount { get; }

    /// <summary>
    /// Gets a specific ring (outer boundary at index 0, followed by holes).
    /// </summary>
    /// <param name="index">The index of the ring.</param>
    /// <returns>A ReadOnlySpan containing the ring's points.</returns>
    ReadOnlySpan<TPoint> GetRing(int index);
}

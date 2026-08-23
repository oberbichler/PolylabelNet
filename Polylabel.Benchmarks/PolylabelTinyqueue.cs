using System.Runtime.CompilerServices;
using Polylabel;

namespace Polylabel.Benchmarks;

internal readonly struct TinyCellQueue : ICellQueue
{
    private readonly Tinyqueue<Cell> _queue;

    public TinyCellQueue()
    {
        _queue = new Tinyqueue<Cell>(compare: (a, b) => b.Max.CompareTo(a.Max));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(Cell cell) => _queue.Push(cell);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cell Dequeue() => _queue.Pop();

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _queue.Length;
    }
}

public static class PolylabelTinyqueue
{
    public static PolylabelResult Run(Polygon polygon, double precision = 1.0, Action<string>? trace = null)
    {
        return PoleOfInaccessibility.FindCore<Polygon, Point, TinyCellQueue>(polygon, new TinyCellQueue(), precision, trace);
    }
}

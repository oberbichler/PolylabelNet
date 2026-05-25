using System.Runtime.CompilerServices;

namespace Polylabel;

internal readonly struct Cell
{
    public double X { get; }
    public double Y { get; }
    public double H { get; }
    public double D { get; }
    public double Max { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cell(double x, double y, double h, double d)
    {
        X = x;
        Y = y;
        H = h;
        D = d;
        Max = d + h * 1.4142135623730951; // SQRT2
    }
}

internal readonly struct MaxDoubleComparer : IComparer<double>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(double x, double y) => y.CompareTo(x);
}

internal interface ICellQueue
{
    void Enqueue(Cell cell);
    Cell Dequeue();
    int Count { get; }
}

internal readonly struct NativeCellQueue : ICellQueue
{
    private readonly PriorityQueue<Cell, double> _queue;

    public NativeCellQueue()
    {
        _queue = new PriorityQueue<Cell, double>(new MaxDoubleComparer());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(Cell cell) => _queue.Enqueue(cell, cell.Max);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cell Dequeue() => _queue.Dequeue();

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _queue.Count;
    }
}

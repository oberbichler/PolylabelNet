using System;
using System.Collections.Generic;

namespace Polylabel.Benchmarks;

public class Tinyqueue<T>
{
    private readonly List<T> _data;
    private readonly Comparison<T> _compare;

    public int Length => _data.Count;

    public Tinyqueue(IEnumerable<T>? data = null, Comparison<T>? compare = null)
    {
        _data = data != null ? new List<T>(data) : new List<T>();
        _compare = compare ?? Comparer<T>.Default.Compare;

        if (_data.Count > 1)
        {
            for (int i = (_data.Count >> 1) - 1; i >= 0; i--)
            {
                Down(i);
            }
        }
    }

    public void Push(T item)
    {
        _data.Add(item);
        Up(_data.Count - 1);
    }

    public T Pop()
    {
        if (_data.Count == 0) throw new InvalidOperationException("Queue is empty.");

        T top = _data[0];
        T bottom = _data[^1];
        _data.RemoveAt(_data.Count - 1);

        if (_data.Count > 0)
        {
            _data[0] = bottom;
            Down(0);
        }

        return top;
    }

    public T Peek()
    {
        if (_data.Count == 0) throw new InvalidOperationException("Queue is empty.");
        return _data[0];
    }

    private void Up(int pos)
    {
        T item = _data[pos];

        while (pos > 0)
        {
            int parent = (pos - 1) >> 1;
            T current = _data[parent];

            if (_compare(item, current) >= 0) break;

            _data[pos] = current;
            pos = parent;
        }

        _data[pos] = item;
    }

    private void Down(int pos)
    {
        T item = _data[pos];
        int len = _data.Count;
        int half = len >> 1;

        while (pos < half)
        {
            int left = (pos << 1) + 1;
            int right = left + 1;
            T best = _data[left];

            if (right < len && _compare(_data[right], best) < 0)
            {
                left = right;
                best = _data[right];
            }

            if (_compare(best, item) >= 0) break;

            _data[pos] = best;
            pos = left;
        }

        _data[pos] = item;
    }
}

using Tyr.Common.Collections;

namespace Tyr.Common.Math;

public class MedianFilter<T>(int size = 10) where T : unmanaged, IComparable<T>
{
    private readonly CircularBuffer<T> _buffer = new(size);

    public void Add(T value)
    {
        _buffer.Add(value);

        Span<T> sortBuffer = stackalloc T[_buffer.Count];
        for (var i = 0; i < _buffer.Count; i++)
        {
            sortBuffer[i] = _buffer[i];
        }

        sortBuffer.Sort();
        Current = sortBuffer[sortBuffer.Length / 2];
    }

    public T Current { get; private set; }

    public void Reset()
    {
        _buffer.Clear();
        Current = default;
    }
}
namespace Tyr.Common.Math;

public class MedianFilter<T>(int size = 10)
    where T : IComparable<T>
{
    private readonly T[] _buffer = new T[size];
    private readonly T[] _sortBuffer = new T[size];

    private int _index = 0;
    private bool _empty = true;

    public void Add(T value)
    {
        if (_empty)
        {
            Array.Fill(_buffer, value);
            _empty = false;
        }

        _buffer[_index] = value;
        _index = (_index + 1) % _buffer.Length;
    }

    public T Current()
    {
        Array.Copy(_buffer, _sortBuffer, size);
        Array.Sort(_sortBuffer);
        return _sortBuffer[size / 2];
    }

    public void Reset()
    {
        _index = 0;
        _empty = true;
    }
}
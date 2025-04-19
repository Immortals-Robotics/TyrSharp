using System.Collections;

namespace Tyr.Common.Collections;

public class CircularBuffer<T>(int capacity) : IEnumerable<T>
{
    private readonly T[] _buffer = new T[capacity];

    private int _index;

    public int Capacity => _buffer.Length;

    public int Count { get; private set; }

    public bool IsFull => Count == Capacity;
    public bool IsEmpty => Count == 0;

    // index should be in [0, Count)
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Index {index} out of range for buffer size {Count}.");
            }

            return _buffer[InternalIndex(index)];
        }
        set
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Index {index} out of range for buffer size {Count}.");
            }

            _buffer[InternalIndex(index)] = value;
        }
    }

    public void Add(T item)
    {
        _buffer[_index] = item;
        _index = (_index + 1) % Capacity;
        Count = System.Math.Min(Count + 1, Capacity);
    }

    public void Clear()
    {
        _index = 0;
        Count = 0;
        Array.Clear(_buffer, 0, _buffer.Length);
    }

    #region IEnumerable<T> implementation

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    #endregion

    #region IEnumerable implementation

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion

    private int InternalIndex(int index)
    {
        return (index + _index + Capacity - Count) % Capacity;
    }
}
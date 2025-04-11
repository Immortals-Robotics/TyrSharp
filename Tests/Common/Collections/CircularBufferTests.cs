using Tyr.Common.Collections;

namespace Tyr.Tests.Common.Collections;

public class CircularBufferTests
{
    [Fact]
    public void Buffer_IsEmpty_Initially()
    {
        var buffer = new CircularBuffer<int>(3);
        Assert.True(buffer.IsEmpty);
        Assert.False(buffer.IsFull);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Buffer_Adds_And_Reads_Items_Correctly()
    {
        var buffer = new CircularBuffer<string>(3);
        buffer.Add("a");
        buffer.Add("b");

        Assert.Equal(2, buffer.Count);
        Assert.Equal("a", buffer[0]);
        Assert.Equal("b", buffer[1]);
    }

    [Fact]
    public void Buffer_Overwrites_When_Full()
    {
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4); // overwrites 1

        Assert.True(buffer.IsFull);
        Assert.Equal(3, buffer.Count);
        Assert.Equal(2, buffer[0]);
        Assert.Equal(3, buffer[1]);
        Assert.Equal(4, buffer[2]);
    }

    [Fact]
    public void Buffer_Indexer_Throws_On_OutOfRange()
    {
        var buffer = new CircularBuffer<int>(2);
        buffer.Add(42);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[1]);
    }

    [Fact]
    public void Buffer_Enumerator_Yields_Logical_Order()
    {
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4); // overwrites 1

        var items = buffer.ToList(); // uses IEnumerable<T>

        Assert.Equal(new List<int> { 2, 3, 4 }, items);
    }

    [Fact]
    public void Buffer_Clear_Resets_State()
    {
        var buffer = new CircularBuffer<float>(5);
        buffer.Add(1.1f);
        buffer.Add(2.2f);

        buffer.Clear();

        Assert.True(buffer.IsEmpty);
        Assert.Equal(0, buffer.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[0]);
    }

    [Fact]
    public void Buffer_Indexer_Can_Set_Value()
    {
        var buffer = new CircularBuffer<string>(2);
        buffer.Add("hello");
        buffer.Add("world");

        buffer[1] = "tyr";
        Assert.Equal("tyr", buffer[1]);
    }
}
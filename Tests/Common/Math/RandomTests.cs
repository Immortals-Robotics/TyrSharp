using Tyr.Common.Math.Extensions;

namespace Tyr.Tests.Common.Math;

public class RandomTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 10)]
    [InlineData(-10, 0)]
    public void Int_Get_ReturnsInRange(int min, int max)
    {
        var rng = new Random();
        for (var i = 0; i < 1000; i++)
        {
            var val = rng.Get(min, max);
            Assert.InRange(val, min, max - 1);
        }
    }

    [Fact]
    public void Get_FromList_ReturnsValidElement()
    {
        var rng = new Random();
        var list = new[] { "a", "b", "c" };
        for (var i = 0; i < 1000; i++)
            Assert.Contains(rng.Get(list), list);
    }

    [Fact]
    public void Shuffle_PreservesAllElements()
    {
        var rng = new Random();
        var original = Enumerable.Range(0, 100).ToArray();
        var shuffled = original.ToArray();

        rng.Shuffle(shuffled);

        Assert.Equal(original.Length, shuffled.Length);
        Assert.True(original.All(i => shuffled.Contains(i)));
    }

    [Fact]
    public void Seed_ProducesRepeatableSequence()
    {
        var rng1 = new Random(42);
        var rng2 = new Random(42);

        var sequence1 = Enumerable.Range(0, 10).Select(_ => rng1.Get(0, 100)).ToArray();
        var sequence2 = Enumerable.Range(0, 10).Select(_ => rng2.Get(0, 100)).ToArray();

        Assert.Equal(sequence1, sequence2);
    }

    [Fact]
    public void Enum_Get_ReturnsValidEnum()
    {
        var rng = new Random();
        for (var i = 0; i < 100; i++)
        {
            var val = rng.Get<DayOfWeek>();
            Assert.IsType<DayOfWeek>(val);
        }
    }
}
using Tyr.Common.Math;

namespace Tyr.Tests.Common.Math;

public class MedianFilterTests
{
    [Fact]
    public void Median_Of_Constant_Values_Is_Constant()
    {
        var filter = new MedianFilter<float>(5);

        for (var i = 0; i < 5; i++)
            filter.Add(3f);

        Assert.Equal(3f, filter.Current, 3);
    }

    [Fact]
    public void Median_Of_Odd_Sequence_Is_Correct()
    {
        var filter = new MedianFilter<int>(5);
        filter.Add(1);
        filter.Add(2);
        filter.Add(3);
        filter.Add(4);
        filter.Add(5);

        Assert.Equal(3, filter.Current);
    }

    [Fact]
    public void Median_Stabilizes_Quickly_With_Extremes()
    {
        var filter = new MedianFilter<int>(5);

        // Fill with 0s first
        filter.Add(0);
        filter.Add(0);
        filter.Add(0);
        filter.Add(0);
        filter.Add(0);

        // Now add a few outliers
        filter.Add(100);
        filter.Add(100);
        filter.Add(100);

        Assert.Equal(100, filter.Current); // buffer is now [100,100,100,0,0] → median = 100
    }

    [Fact]
    public void Reset_Clears_Internal_State()
    {
        var filter = new MedianFilter<int>(3);
        filter.Add(10);
        filter.Add(20);
        filter.Add(30);

        Assert.Equal(20, filter.Current);

        filter.Reset();
        filter.Add(1);

        // Since it refills the buffer with 1 on first sample
        Assert.Equal(1, filter.Current);
    }

    [Fact]
    public void MedianFilter_Can_Handle_Floats()
    {
        var filter = new MedianFilter<float>(3);
        filter.Add(1.1f);
        filter.Add(2.2f);
        filter.Add(3.3f);

        Assert.Equal(2.2f, filter.Current, 3);
    }

    [Fact]
    public void MedianFilter_Handles_Negatives()
    {
        var filter = new MedianFilter<int>(5);
        filter.Add(-10);
        filter.Add(-5);
        filter.Add(0);
        filter.Add(5);
        filter.Add(10);

        Assert.Equal(0, filter.Current);
    }
}
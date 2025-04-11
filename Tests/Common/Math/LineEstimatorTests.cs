using Tyr.Common.Math;

namespace Tyr.Tests.Common.Math;

public class LineEstimatorTests
{
    [Fact]
    public void Estimate_ReturnsNull_IfLessThanTwoSamples()
    {
        var estimator = new LineEstimator(5);
        estimator.AddSample(1f, 2f);
        Assert.Null(estimator.Estimate);
    }

    [Fact]
    public void Estimate_ExactFit_TwoPoints()
    {
        var estimator = new LineEstimator(2);
        estimator.AddSample(1f, 2f); // y = 2 = 1 + 1
        estimator.AddSample(2f, 3f); // y = 3 = 1 + 2

        var result = estimator.Estimate;
        Assert.NotNull(result);
        Assert.Equal(1f, result.Value.slope, 3);
        Assert.Equal(1f, result.Value.intercept, 3);
    }

    [Fact]
    public void Estimate_CorrectlyHandlesOverwriting()
    {
        var estimator = new LineEstimator(3);

        estimator.AddSample(1f, 1f);
        estimator.AddSample(2f, 2f);
        estimator.AddSample(3f, 3f);
        estimator.AddSample(4f, 4f); // overwrites (1,1)

        var result = estimator.Estimate;
        Assert.NotNull(result);
        Assert.Equal(1f, result.Value.slope, 3);
        Assert.Equal(0f, result.Value.intercept, 3);
    }

    [Fact]
    public void Estimate_ReturnsToNull_AfterReset()
    {
        var estimator = new LineEstimator(5);
        estimator.AddSample(1f, 2f);
        estimator.AddSample(2f, 3f);

        Assert.NotNull(estimator.Estimate);

        estimator.Reset();
        Assert.Null(estimator.Estimate);
        Assert.Equal(0, estimator.Count);
    }
}
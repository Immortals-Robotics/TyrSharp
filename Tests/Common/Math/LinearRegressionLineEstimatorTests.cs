using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Tests.Common.Math;

public class LinearRegressionLineEstimatorTests
{
    [Fact]
    public void Estimate_ReturnsNull_IfLessThanTwoSamples()
    {
        var estimator = new LinearRegressionLineEstimator(5);
        estimator.AddSample(1f, 2f);
        Assert.Null(estimator.Estimate);
    }

    [Fact]
    public void Estimate_ExactFit_TwoPoints()
    {
        var estimator = new LinearRegressionLineEstimator(2);
        estimator.AddSample(1f, 2f);
        estimator.AddSample(2f, 3f);

        var result = estimator.Estimate;
        Assert.NotNull(result);
        Assert.Equal(1f, result.Value.Slope, 3);
        Assert.Equal(1f, result.Value.Intercept, 3);
    }

    [Fact]
    public void Estimate_CorrectlyHandlesOverwriting()
    {
        var estimator = new LinearRegressionLineEstimator(3);

        estimator.AddSample(1f, 1f);
        estimator.AddSample(2f, 2f);
        estimator.AddSample(3f, 3f);
        estimator.AddSample(4f, 4f);

        var result = estimator.Estimate;
        Assert.NotNull(result);
        Assert.Equal(1f, result.Value.Slope, 3);
        Assert.Equal(0f, result.Value.Intercept, 3);
    }

    [Theory]
    [InlineData(0.0f, 15f, 71)]
    [InlineData(0.5f, -20f, 73)]
    [InlineData(-1.0f, 40f, 79)]
    [InlineData(3.0f, 5f, 83)]
    public void Estimate_FitsNoisySlopeInterceptSamples(float slope, float intercept, int seed)
    {
        var samples = LineTestData.CreateNoisySlopeInterceptSamples(
            slope,
            intercept,
            count: 40,
            xStart: -100f,
            xStep: 10f,
            yNoiseStdDev: 3f,
            seed: seed);

        var estimator = CreateEstimator(samples);

        var result = estimator.Estimate;
        Assert.NotNull(result);
        Assert.InRange(result.Value.Slope, slope - 0.05f, slope + 0.05f);
        Assert.InRange(result.Value.Intercept, intercept - 1f, intercept + 1f);
    }

    [Fact]
    public void Estimate_IsInvariantToSampleOrder_ForNoisySlopeInterceptSamples()
    {
        var samples = LineTestData.CreateNoisySlopeInterceptSamples(
            slope: 1.25f,
            intercept: -7f,
            count: 36,
            xStart: -50f,
            xStep: 6f,
            yNoiseStdDev: 2f,
            seed: 89);

        var forward = CreateEstimator(samples).Estimate;
        var reverse = CreateEstimator(samples.AsEnumerable().Reverse().ToList()).Estimate;

        Assert.NotNull(forward);
        Assert.NotNull(reverse);
        Assert.Equal(forward.Value.Slope, reverse.Value.Slope, 5);
        Assert.Equal(forward.Value.Intercept, reverse.Value.Intercept, 5);
    }

    [Fact]
    public void Estimate_ReturnsNull_ForVerticalSamples()
    {
        var estimator = new LinearRegressionLineEstimator(3);
        estimator.AddSample(2f, 1f);
        estimator.AddSample(2f, 2f);
        estimator.AddSample(2f, 3f);

        Assert.Null(estimator.Estimate);
    }

    [Fact]
    public void Estimate_ReturnsToNull_AfterReset()
    {
        var estimator = new LinearRegressionLineEstimator(5);
        estimator.AddSample(1f, 2f);
        estimator.AddSample(2f, 3f);

        Assert.NotNull(estimator.Estimate);

        estimator.Reset();
        Assert.Null(estimator.Estimate);
        Assert.Equal(0, estimator.Count);
    }

    private static LinearRegressionLineEstimator CreateEstimator(IReadOnlyList<Vector2> samples)
    {
        var estimator = new LinearRegressionLineEstimator(samples.Count);
        foreach (var sample in samples)
        {
            estimator.AddSample(sample.X, sample.Y);
        }

        return estimator;
    }
}

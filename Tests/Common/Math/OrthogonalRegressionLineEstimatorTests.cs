using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

namespace Tyr.Tests.Common.Math;

public class OrthogonalRegressionLineEstimatorTests
{
    [Fact]
    public void Estimate_ReturnsNull_IfLessThanTwoSamples()
    {
        var estimator = new OrthogonalRegressionLineEstimator(5);
        estimator.AddSample(1f, 2f);
        Assert.Null(estimator.Estimate);
    }

    [Fact]
    public void Estimate_ExactFit_TwoPoints()
    {
        var estimator = new OrthogonalRegressionLineEstimator(2);
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
        var estimator = new OrthogonalRegressionLineEstimator(3);

        estimator.AddSample(1f, 1f);
        estimator.AddSample(2f, 2f);
        estimator.AddSample(3f, 3f);
        estimator.AddSample(4f, 4f);

        var result = estimator.Estimate;
        Assert.NotNull(result);
        Assert.Equal(1f, result.Value.Slope, 3);
        Assert.Equal(0f, result.Value.Intercept, 3);
    }

    [Fact]
    public void Estimate_ExactFit_VerticalLine()
    {
        var estimator = new OrthogonalRegressionLineEstimator(3);
        estimator.AddSample(2f, 1f);
        estimator.AddSample(2f, 2f);
        estimator.AddSample(2f, 3f);

        var result = estimator.Estimate;
        Assert.NotNull(result);
        Assert.True(float.IsPositiveInfinity(result.Value.Slope));
        Assert.Equal(2f, result.Value.X(0f), 3);
        Assert.True(result.Value.Distance(new Vector2(2f, -10f)) < 0.001f);
    }

    [Theory]
    [InlineData(0.0f, 17)]
    [InlineData(0.5f, 23)]
    [InlineData(-1.0f, 29)]
    [InlineData(3.0f, 31)]
    public void Estimate_FitsNoisyLineAcrossSlopes(float slope, int seed)
    {
        var expectedLine = Line.FromSlopeAndIntercept(slope, 25f);
        var samples = LineTestData.CreateNoisySamples(
            pointOnLine: new Vector2(0f, 25f),
            direction: expectedLine.Direction,
            count: 40,
            spacing: 18f,
            tangentNoiseStdDev: 1.5f,
            normalNoiseStdDev: 5f,
            seed: seed);

        var estimator = CreateEstimator(samples);

        var result = estimator.Estimate;
        Assert.NotNull(result);
        Assert.True(LineTestData.DirectionSimilarity(result.Value.Direction, expectedLine.Direction) > 0.998f);
        Assert.True(result.Value.Distance(new Vector2(0f, 25f)) < 4f);
    }

    [Fact]
    public void Estimate_FitsNoisyNearVerticalLine()
    {
        var expectedPoint = new Vector2(120f, -80f);
        var expectedDirection = Vector2.Normalize(new Vector2(0.03f, 1f));
        var samples = LineTestData.CreateNoisySamples(
            expectedPoint,
            expectedDirection,
            count: 50,
            spacing: 20f,
            tangentNoiseStdDev: 1f,
            normalNoiseStdDev: 4f,
            seed: 41);

        var estimator = CreateEstimator(samples);

        var result = estimator.Estimate;
        Assert.NotNull(result);
        Assert.True(LineTestData.DirectionSimilarity(result.Value.Direction, expectedDirection) > 0.999f);
        Assert.True(result.Value.Distance(expectedPoint) < 4f);
    }

    [Fact]
    public void Estimate_FitsNoisyVerticalLine()
    {
        var expectedPoint = new Vector2(35f, 15f);
        var samples = LineTestData.CreateNoisySamples(
            expectedPoint,
            Vector2.UnitY,
            count: 50,
            spacing: 16f,
            tangentNoiseStdDev: 1f,
            normalNoiseStdDev: 3f,
            seed: 43);

        var estimator = CreateEstimator(samples);

        var result = estimator.Estimate;
        Assert.NotNull(result);
        Assert.True(LineTestData.DirectionSimilarity(result.Value.Direction, Vector2.UnitY) > 0.999f);
        Assert.True(result.Value.Distance(expectedPoint) < 3f);
        Assert.True(MathF.Abs(result.Value.Direction.X) < 0.03f);
    }

    [Fact]
    public void Estimate_CorrectlyTracksLatestNoisySegment_WhenRollingWindowOverwrites()
    {
        var estimator = new OrthogonalRegressionLineEstimator(24);

        foreach (var sample in LineTestData.CreateNoisySamples(
                     pointOnLine: new Vector2(-100f, 0f),
                     direction: Vector2.UnitX,
                     count: 24,
                     spacing: 10f,
                     tangentNoiseStdDev: 1f,
                     normalNoiseStdDev: 2f,
                     seed: 47))
        {
            estimator.AddSample(sample.X, sample.Y);
        }

        foreach (var sample in LineTestData.CreateNoisySamples(
                     pointOnLine: new Vector2(30f, 60f),
                     direction: Vector2.Normalize(new Vector2(0.1f, 1f)),
                     count: 24,
                     spacing: 10f,
                     tangentNoiseStdDev: 1f,
                     normalNoiseStdDev: 2f,
                     seed: 53))
        {
            estimator.AddSample(sample.X, sample.Y);
        }

        var result = estimator.Estimate;
        Assert.NotNull(result);
        Assert.True(LineTestData.DirectionSimilarity(result.Value.Direction, Vector2.Normalize(new Vector2(0.1f, 1f))) > 0.999f);
        Assert.True(result.Value.Distance(new Vector2(30f, 60f)) < 3f);
    }

    [Fact]
    public void Estimate_IsInvariantToSampleOrder_ForNoisyLine()
    {
        var expectedDirection = Vector2.Normalize(new Vector2(1f, -0.75f));
        var samples = LineTestData.CreateNoisySamples(
            pointOnLine: new Vector2(40f, -20f),
            direction: expectedDirection,
            count: 36,
            spacing: 15f,
            tangentNoiseStdDev: 1f,
            normalNoiseStdDev: 4f,
            seed: 59);

        var forward = CreateEstimator(samples).Estimate;
        var reverse = CreateEstimator(samples.AsEnumerable().Reverse().ToList()).Estimate;

        Assert.NotNull(forward);
        Assert.NotNull(reverse);
        Assert.True(LineTestData.DirectionSimilarity(forward.Value.Direction, reverse.Value.Direction) > 0.9999f);
        Assert.True(forward.Value.Distance(new Vector2(40f, -20f)) < 4f);
        Assert.True(reverse.Value.Distance(new Vector2(40f, -20f)) < 4f);
    }

    [Fact]
    public void Estimate_ReturnsToNull_AfterReset()
    {
        var estimator = new OrthogonalRegressionLineEstimator(5);
        estimator.AddSample(1f, 2f);
        estimator.AddSample(2f, 3f);

        Assert.NotNull(estimator.Estimate);

        estimator.Reset();
        Assert.Null(estimator.Estimate);
        Assert.Equal(0, estimator.Count);
    }

    private static OrthogonalRegressionLineEstimator CreateEstimator(IReadOnlyList<Vector2> samples)
    {
        var estimator = new OrthogonalRegressionLineEstimator(samples.Count);
        foreach (var sample in samples)
        {
            estimator.AddSample(sample.X, sample.Y);
        }

        return estimator;
    }
}

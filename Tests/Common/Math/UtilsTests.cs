﻿using Tyr.Common.Math;

namespace Tyr.Tests.Common.Math;

public class UtilsTests
{
    [Theory]
    [InlineData(1.000f, 1.0001f, true)]
    [InlineData(1.000f, 1.002f, false)]
    [InlineData(0f, 0f, true)]
    [InlineData(-1.000f, -1.0001f, true)]
    [InlineData(-1.000f, -1.01f, false)]
    public void AlmostEqual_DefaultEpsilon_WorksCorrectly(float a, float b, bool expected)
    {
        Assert.Equal(expected, Utils.ApproximatelyEqual(a, b));
    }

    [Fact]
    public void AlmostEqual_CustomEpsilon_Works()
    {
        var a = 5.0f;
        var b = 5.5f;
        var epsilon = 0.6f;
        Assert.True(Utils.ApproximatelyEqual(a, b, epsilon));

        epsilon = 0.4f;
        Assert.False(Utils.ApproximatelyEqual(a, b, epsilon));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.0001f, 1)]
    [InlineData(-0.0001f, -1)]
    [InlineData(42.0f, 1)]
    [InlineData(-123.45f, -1)]
    public void SignInt_ReturnsCorrectSign(float input, int expected)
    {
        Assert.Equal(expected, Utils.SignInt(input));
    }
}
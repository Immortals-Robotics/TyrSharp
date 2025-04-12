using Tyr.Common.Math;

namespace Tyr.Tests.Common.Math;

public class Vector2Tests
{
    [Fact]
    public void ZeroVector_HasZeroLength()
    {
        var v = Vector2.Zero;
        Assert.Equal(0f, v.Length(), 3);
        Assert.Equal(0f, v.LengthSquared(), 3);
    }

    [Fact]
    public void Normalized_WorksCorrectly()
    {
        var v = new Vector2(3, 4);
        var n = v.Normalized();
        Assert.Equal(1f, n.Length(), 3);
        Assert.Equal(0.6f, n.X, 3);
        Assert.Equal(0.8f, n.Y, 3);
    }

    [Fact]
    public void DotProduct_IsCorrect()
    {
        var a = new Vector2(1, 0);
        var b = new Vector2(0, 1);
        Assert.Equal(0f, a.Dot(b), 3);

        var c = new Vector2(3, 4);
        Assert.Equal(25f, c.Dot(c), 3); // dot = length²
    }

    [Fact]
    public void CrossProduct_IsCorrect()
    {
        var a = new Vector2(1, 0);
        var b = new Vector2(0, 1);
        Assert.Equal(1f, a.Cross(b), 3);
        Assert.Equal(-1f, b.Cross(a), 3);
    }

    [Fact]
    public void Distance_Calculations_AreCorrect()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(3, 4);
        Assert.Equal(5f, a.DistanceTo(b), 3);
        Assert.Equal(25f, a.DistanceSquaredTo(b), 3);
    }

    [Fact]
    public void Rotated_ReturnsSameLength()
    {
        var a = new Vector2(1, 0);
        var r = a.Rotated(Angle.FromDeg(90));
        Assert.Equal(1f, r.Length(), 3);
        Assert.Equal(0f, r.X, 3);
        Assert.Equal(1f, r.Y, 3);
    }

    [Fact]
    public void AngleWith_ReturnsCorrectRelativeAngle()
    {
        var a = new Vector2(1, 0);
        var b = new Vector2(0, 1);
        var angle = a.AngleWith(b);
        Assert.Equal(135f, angle.Deg);
    }

    [Fact]
    public void AngleDiff_ReturnsCorrectSignedAngle()
    {
        var a = new Vector2(1, 0);
        var b = new Vector2(0, 1);
        var diff = a.AngleDiff(b);
        Assert.Equal(90f, diff.Deg);
    }

    [Fact]
    public void CircleAroundPoint_ProducesCorrectRadius()
    {
        var origin = new Vector2(0, 0);
        var p = origin.CircleAroundPoint(Angle.FromDeg(0), 10);
        Assert.Equal(10f, p.X, 3);
        Assert.Equal(0f, p.Y, 3);
    }

    [Fact]
    public void PointOnConnectingLine_ApproximateMidpoint()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(10, 10);
        var p = a.PointOnConnectingLine(b, 7.07f); // ~sqrt(2)/2 * 10

        Assert.Equal(5f, p.X, 1);
        Assert.Equal(5f, p.Y, 1);
    }

    [Fact]
    public void PointOnConnectingLine_VerticalLine()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(0, 10);

        var p = a.PointOnConnectingLine(b, 1f);

        Assert.Equal(0f, p.X);
        Assert.Equal(1f, p.Y);
    }

    [Fact]
    public void Operators_WorkCorrectly()
    {
        var a = new Vector2(1, 2);
        var b = new Vector2(3, 4);
        var sum = a + b;
        Assert.Equal(4f, sum.X);
        Assert.Equal(6f, sum.Y);

        var scaled = a * 2;
        Assert.Equal(2f, scaled.X);
        Assert.Equal(4f, scaled.Y);

        var flipped = -a;
        Assert.Equal(-1f, flipped.X);
        Assert.Equal(-2f, flipped.Y);
    }

    [Fact]
    public void Equality_UsesAlmostEqual()
    {
        var a = new Vector2(1.000f, 2.000f);
        var b = new Vector2(1.0009f, 2.0009f); // within epsilon
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Clamp_ReturnsCorrectlyBoundedVector()
    {
        var v = new Vector2(5, -5);
        var min = new Vector2(0, 0);
        var max = new Vector2(4, 4);
        var clamped = Vector2.Clamp(v, min, max);
        Assert.Equal(4f, clamped.X);
        Assert.Equal(0f, clamped.Y);
    }

    [Fact]
    public void MaxMin_ReturnsCorrectResults()
    {
        var a = new Vector2(1, 5);
        var b = new Vector2(4, 2);
        var max = Vector2.Max(a, b);
        var min = Vector2.Min(a, b);

        Assert.Equal(4f, max.X);
        Assert.Equal(5f, max.Y);

        Assert.Equal(1f, min.X);
        Assert.Equal(2f, min.Y);
    }

    [Fact]
    public void Accessors_WorkCorrectly()
    {
        var v = new Vector2(3, 7);
        Assert.Equal(new Vector2(3, 3), v.Xx());
        Assert.Equal(new Vector2(7, 7), v.Yy());
        Assert.Equal(new Vector2(7, 3), v.Yx());
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var v = new Vector2(1.5f, -2f);
        Assert.Equal("[1.5, -2]", v.ToString());
    }
}
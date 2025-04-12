using System.Globalization;
using Tyr.Common.Math;

namespace Tyr.Tests.Common.Math;

public class Vector3Tests
{
    static Vector3Tests()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
    }
    
    [Fact]
    public void ZeroVector_HasZeroLength()
    {
        var v = Vector3.Zero;
        Assert.Equal(0f, v.Length(), 3);
        Assert.Equal(0f, v.LengthSquared(), 3);
    }

    [Fact]
    public void Normalized_ReturnsUnitLengthVector()
    {
        var v = new Vector3(3, 4, 0);
        var n = v.Normalized();

        Assert.Equal(1f, n.Length(), 3);
        
        Assert.Equal(0.6f, n.X);
        Assert.Equal(0.8f, n.Y);
        Assert.Equal(0f, n.Z);
    }

    [Fact]
    public void Normalized_OfZeroVector_IsZero()
    {
        var n = Vector3.Zero.Normalized();
        Assert.Equal(Vector3.Zero, n);
    }

    [Fact]
    public void Dot_ReturnsCorrectResult()
    {
        var a = new Vector3(1, 2, 3);
        var b = new Vector3(4, -5, 6);

        float expected = 1 * 4 + 2 * -5 + 3 * 6;
        Assert.Equal(expected, a.Dot(b), 5);
    }

    [Fact]
    public void Cross_ReturnsCorrectResult()
    {
        var a = new Vector3(1, 0, 0);
        var b = new Vector3(0, 1, 0);
        var cross = a.Cross(b);

        Assert.Equal(new Vector3(0, 0, 1), cross);
    }

    [Fact]
    public void Cross_IsPerpendicularToInputs()
    {
        var a = new Vector3(1, 2, 3);
        var b = new Vector3(4, 5, 6);
        var cross = a.Cross(b);

        Assert.Equal(0f, a.Dot(cross));
        Assert.Equal(0f, b.Dot(cross));
    }

    [Fact]
    public void Distance_ComputesCorrectly()
    {
        var a = new Vector3(1, 2, 3);
        var b = new Vector3(4, 6, 3);

        Assert.Equal(5f, a.DistanceTo(b), 3);
        Assert.Equal(25f, a.DistanceSquaredTo(b), 3);
    }

    [Fact]
    public void Operators_WorkAsExpected()
    {
        var a = new Vector3(1, 2, 3);
        var b = new Vector3(4, 5, 6);

        Assert.Equal(new Vector3(5, 7, 9), a + b);
        Assert.Equal(new Vector3(-3, -3, -3), a - b);
        Assert.Equal(new Vector3(4, 10, 18), a * b);
        Assert.Equal(new Vector3(0.25f, 0.4f, 0.5f), a / b);
        Assert.Equal(new Vector3(2, 4, 6), a * 2);
        Assert.Equal(new Vector3(0.5f, 1f, 1.5f), a / 2);
        Assert.Equal(new Vector3(-1, -2, -3), -a);
        Assert.Equal(a, +a);
    }

    [Fact]
    public void Equality_UsesAlmostEqual()
    {
        var a = new Vector3(1.0001f, 2.0001f, 3.0001f);
        var b = new Vector3(1.0002f, 2.0002f, 3.0002f);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals((object)b));
    }

    [Fact]
    public void Xy_ReturnsCorrectVector2Projection()
    {
        var v = new Vector3(1, 2, 3);
        var xy = v.Xy();
        Assert.Equal(new Vector2(1, 2), xy);
    }

    [Fact]
    public void Abs_ReturnsComponentwiseAbsolute()
    {
        var v = new Vector3(-1, -2, 3);
        Assert.Equal(new Vector3(1, 2, 3), v.Abs());
    }

    [Fact]
    public void ToString_FormatsAsExpected()
    {
        var v = new Vector3(1.23f, -4.56f, 7.89f);
        Assert.Equal("[1.23, -4.56, 7.89]", v.ToString());
    }

    [Fact]
    public void HashCode_IsStableForEqualVectors()
    {
        var a = new Vector3(1, 2, 3);
        var b = new Vector3(1, 2, 3);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
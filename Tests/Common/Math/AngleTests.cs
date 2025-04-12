using System.Globalization;
using Tyr.Common.Math;

namespace Tyr.Tests.Common.Math;

public class AngleTests
{
    static AngleTests()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
    }
    
    [Fact]
    public void FromDeg_CreatesNormalizedAngle()
    {
        Assert.Equal(90f, Angle.FromDeg(90).Deg, 3);
        Assert.Equal(-90f, Angle.FromDeg(270).Deg, 3); // normalized to [-180, 180]
        Assert.Equal(0f, Angle.FromDeg(360).Deg, 3);
        Assert.Equal(0f, Angle.FromDeg(-720).Deg, 3);
    }

    [Fact]
    public void FromRad_CreatesCorrectAngle()
    {
        var angle = Angle.FromRad(MathF.PI);
        Assert.Equal(180f, angle.Deg, 3);
    }

    [Fact]
    public void ToUnitVec_ReturnsCorrectDirection()
    {
        var angle = Angle.FromDeg(90);
        var vec = angle.ToUnitVec();
        Assert.Equal(0f, vec.X, 3);
        Assert.Equal(1f, vec.Y, 3);
    }

    [Fact]
    public void FromVector_CreatesAngleFromDirection()
    {
        var angle = Angle.FromVector(new Vector2(1, 0)); // → 0°
        Assert.Equal(0f, angle.Deg, 3);

        angle = Angle.FromVector(new Vector2(0, 1)); // ↑ 90°
        Assert.Equal(90f, angle.Deg, 3);
    }

    [Fact]
    public void Average_ReturnsCorrectAngle()
    {
        var a = Angle.FromDeg(0);
        var b = Angle.FromDeg(90);
        var avg = Angle.Average(a, b);
        Assert.True(avg.Deg is > 35 and < 55);
    }

    [Fact]
    public void Equality_WorksWithAlmostEqual()
    {
        var a = Angle.FromDeg(10);
        var b = Angle.FromDeg(370); // same angle
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void ComparisonOperators_WorkAsExpected()
    {
        var a = Angle.FromDeg(10);
        var b = Angle.FromDeg(90);
        Assert.True(a < b);
        Assert.False(b < a);
        Assert.True(b > a);
    }

    [Fact]
    public void IsBetween_Works_WithoutWraparound()
    {
        var angle = Angle.FromDeg(45);
        var low = Angle.FromDeg(30);
        var high = Angle.FromDeg(60);

        Assert.True(angle.IsBetween(low, high));
    }

    [Fact]
    public void IsBetween_Works_WithWraparound()
    {
        var angle = Angle.FromDeg(350);
        var low = Angle.FromDeg(340);
        var high = Angle.FromDeg(20);

        Assert.True(angle.IsBetween(low, high));
    }

    [Fact]
    public void Sin_Cos_Tan_WorkCorrectly()
    {
        var a = Angle.FromDeg(0);
        Assert.Equal(0f, a.Sin(), 3);
        Assert.Equal(1f, a.Cos(), 3);

        var b = Angle.FromDeg(90);
        Assert.Equal(1f, b.Sin(), 3);
        Assert.True(MathF.Abs(b.Cos()) < 0.001f);
    }

    [Fact]
    public void ToString_OutputsFormattedDegrees()
    {
        var a = Angle.FromDeg(42.1234f);
        Assert.StartsWith("42.12", a.ToString());
    }
}
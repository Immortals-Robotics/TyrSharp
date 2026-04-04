using System.Numerics;
using Tyr.Common.Math.Shapes;

namespace Tyr.Tests.Common.Math;

public class LineTests
{
    [Fact]
    public void Direction_MatchesLineSlope()
    {
        var line = Line.FromSlopeAndIntercept(2f, 0f);

        Assert.True(Vector2.Dot(line.Direction, Vector2.Normalize(new Vector2(1f, 2f))) > 0.999f);
    }

    [Fact]
    public void Direction_IsVertical_ForVerticalLine()
    {
        var line = Line.FromTwoPoints(new Vector2(3f, -1f), new Vector2(3f, 5f));

        Assert.True(MathF.Abs(line.Direction.X) < 0.001f);
        Assert.True(MathF.Abs(line.Direction.Y) > 0.999f);
    }
}

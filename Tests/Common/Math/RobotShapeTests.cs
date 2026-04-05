using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

namespace Tyr.Tests.Common.Math;

public class RobotShapeTests
{
    [Fact]
    public void GetFrontLine_UsesCenterToDribblerGeometry()
    {
        var robot = new Robot
        {
            Center = Vector2.Zero,
            Radius = 90f,
            CenterToDribbler = 75f,
            Angle = Angle.Zero
        };

        var frontLine = robot.GetFrontLine();
        var expectedHalfWidth = MathF.Sqrt(90f * 90f - 75f * 75f);

        Assert.InRange(frontLine.Start.X, 74.999f, 75.001f);
        Assert.InRange(frontLine.End.X, 74.999f, 75.001f);
        Assert.InRange(MathF.Abs(frontLine.Start.Y), expectedHalfWidth - 0.001f, expectedHalfWidth + 0.001f);
        Assert.InRange(MathF.Abs(frontLine.End.Y), expectedHalfWidth - 0.001f, expectedHalfWidth + 0.001f);
    }

    [Fact]
    public void Inside_ExcludesSpaceInFrontOfDribbler()
    {
        var robot = new Robot
        {
            Center = Vector2.Zero,
            Radius = 90f,
            CenterToDribbler = 75f,
            Angle = Angle.Zero
        };

        Assert.True(robot.Inside(new Vector2(70f, 0f)));
        Assert.False(robot.Inside(new Vector2(80f, 0f)));
    }
}

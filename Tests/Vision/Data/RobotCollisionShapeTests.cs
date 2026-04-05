using System.Numerics;
using Tyr.Common.Math;
using Tyr.Vision.Data;

namespace Tyr.Tests.Vision.Data;

public class RobotCollisionShapeTests
{
    [Fact]
    public void GetCollision_ReflectsBallOnRobotFront()
    {
        var shape = new RobotCollisionShape
        {
            Position = Vector2.Zero,
            Orientation = Angle.Zero,
            Velocity = Vector2.Zero,
            Radius = 90f,
            CenterToDribbler = 75f,
            MaxCircleLoss = 0.6f,
            MaxFrontLoss = 0.5f
        };

        var result = shape.GetCollision(new Vector2(74f, 0f), new Vector2(-1000f, 0f));

        Assert.Equal(RobotCollisionLocation.Front, result.Location);
        Assert.True(result.BallReflectedVelocity.HasValue);
        Assert.InRange(result.BallReflectedVelocity.Value.X, 499f, 501f);
        Assert.InRange(MathF.Abs(result.BallReflectedVelocity.Value.Y), 0f, 0.001f);
    }
}

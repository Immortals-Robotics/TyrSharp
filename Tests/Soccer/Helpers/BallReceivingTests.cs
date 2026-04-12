using System.Numerics;
using Tyr.Common;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Helpers;
using Tyr.Vision.Trajectory;

namespace Tyr.Tests.Soccer.Helpers;

public class BallReceivingTests
{
    public BallReceivingTests()
    {
        ServiceLocator.BallTrajectoryFactory = new BallTrajectoryFactory();
    }

    [Fact]
    public void ProjectReceivePoint_PrefersBackProjection_WhenPreferredPointIsBehindBall()
    {
        var trajectory = new BallFlat(new BallState
        {
            Position3D = Vector3.Zero,
            Velocity = new Vector3(1000f, 0f, 0f),
            Acceleration = Vector3.Zero,
            SpinRadians = Vector2.Zero
        });

        var projection = BallReceiving.ProjectReceivePoint(
            trajectory,
            Vector2.Zero,
            new Vector2(1000f, 0f),
            250f,
            new Vector2(-150f, 20f));

        Assert.True(projection.UsedBackProjection);
        Assert.InRange(projection.Point.X, -150.001f, -149.999f);
        Assert.InRange(projection.Point.Y, -0.001f, 0.001f);
    }

    [Fact]
    public void ProjectReceivePoint_RejectsBackProjection_WhenItIsTooFarFromRollingPath()
    {
        var trajectory = new BallFlat(new BallState
        {
            Position3D = Vector3.Zero,
            Velocity = new Vector3(1000f, 0f, 0f),
            Acceleration = Vector3.Zero,
            SpinRadians = Vector2.Zero
        });

        var preferredReceivePoint = new Vector2(150f, 0f);
        var projection = BallReceiving.ProjectReceivePoint(
            trajectory,
            Vector2.Zero,
            new Vector2(1000f, 0f),
            1500f,
            preferredReceivePoint);

        Assert.False(projection.UsedBackProjection);
        Assert.InRange(projection.Point.X, 149.999f, 150.001f);
        Assert.InRange(projection.Point.Y, -0.001f, 0.001f);
    }

    [Fact]
    public void GetCenterDestination_UsesDribblerAndBallGeometry()
    {
        var destination = BallReceiving.GetCenterDestination(
            Vector2.Zero,
            Angle.Zero,
            75f,
            21.5f);

        Assert.InRange(destination.X, -96.501f, -96.499f);
        Assert.InRange(destination.Y, -0.001f, 0.001f);
    }

    [Fact]
    public void ClampToLegalDestination_PushesPointOutOfPenaltyArea()
    {
        var fieldBounds = Rectangle.FromCenterAndSize(Vector2.Zero, 13400f, 10400f);
        var ownPenalty = Rectangle.FromCornerAndSize(new Vector2(4200f, -1800f), 1800f, 3600f);
        var oppPenalty = Rectangle.FromCornerAndSize(new Vector2(-6000f, -1800f), 1800f, 3600f);

        var clamped = BallReceiving.ClampToLegalDestination(
            new Vector2(5300f, 0f),
            fieldBounds,
            ownPenalty,
            oppPenalty,
            110f);

        Assert.False(ownPenalty.Inside(clamped, 110f));
        Assert.True(fieldBounds.Inside(clamped));
    }

    [Fact]
    public void IsBallMovingTowardsPoint_DetectsApproachingAndRecedingBall()
    {
        Assert.True(BallReceiving.IsBallMovingTowardsPoint(
            Vector2.Zero,
            new Vector2(1000f, 0f),
            new Vector2(200f, 0f)));

        Assert.False(BallReceiving.IsBallMovingTowardsPoint(
            Vector2.Zero,
            new Vector2(-1000f, 0f),
            new Vector2(200f, 0f)));
    }
}

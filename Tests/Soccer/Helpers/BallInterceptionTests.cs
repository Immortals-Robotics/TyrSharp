using System.Numerics;
using Tyr.Common;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Math;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Helpers;
using Tyr.Soccer.Robot;
using Tyr.Vision.Trajectory;

namespace Tyr.Tests.Soccer.Helpers;

public class BallInterceptionTests
{
    public BallInterceptionTests()
    {
        ServiceLocator.BallTrajectoryFactory = new BallTrajectoryFactory();
    }

    [Fact]
    public void TryGetInterceptWindow_UsesTouchdownForAirborneBall()
    {
        var ball = CreateBall(
            new Vector3(0f, 0f, 200f),
            new Vector3(1500f, 0f, 800f));
        var trajectory = ServiceLocator.BallTrajectoryFactory.FromState(ball.State);

        var ok = BallInterception.TryGetInterceptWindow(
            ball,
            trajectory,
            FieldSize.DivisionA.RectangleWithBoundary,
            out var window);

        Assert.True(ok);
        Assert.True(window.StartTimeSeconds > 0f);
        Assert.True(window.EndTimeSeconds >= window.StartTimeSeconds);
    }

    [Fact]
    public void TryGetInterceptWindow_StopsWhenBallLeavesField()
    {
        var ball = CreateBall(
            new Vector3(6500f, 0f, 0f),
            new Vector3(1200f, 0f, 0f));
        var trajectory = ServiceLocator.BallTrajectoryFactory.FromState(ball.State);

        var ok = BallInterception.TryGetInterceptWindow(
            ball,
            trajectory,
            FieldSize.DivisionA.RectangleWithBoundary,
            out var window);

        Assert.True(ok);
        Assert.InRange(window.EndTimeSeconds, 0f, 0.5f);
    }

    [Fact]
    public void TryFindPlan_ChoosesLaterInterceptionForFartherRobot()
    {
        var ball = CreateBall(
            new Vector3(-500f, 0f, 0f),
            new Vector3(1800f, 0f, 0f));
        var trajectory = ServiceLocator.BallTrajectoryFactory.FromState(ball.State);
        var fieldBounds = FieldSize.DivisionA.RectangleWithBoundary;

        var closeOk = BallInterception.TryFindPlan(
            ball,
            trajectory,
            new Vector2(800f, 400f),
            Vector2.Zero,
            Angle.Zero,
            VelocityProfile.Mamooli,
            fieldBounds,
            FieldSize.DivisionA.BallRadius,
            75f,
            out var closePlan);

        var farOk = BallInterception.TryFindPlan(
            ball,
            trajectory,
            new Vector2(800f, 1800f),
            Vector2.Zero,
            Angle.Zero,
            VelocityProfile.Mamooli,
            fieldBounds,
            FieldSize.DivisionA.BallRadius,
            75f,
            out var farPlan);

        Assert.True(closeOk);
        Assert.True(farOk);
        Assert.True(farPlan.TimeSeconds > closePlan.TimeSeconds);
        Assert.InRange(closePlan.AbsSlackTimeSeconds, 0f, 0.35f);
        Assert.InRange(farPlan.AbsSlackTimeSeconds, 0f, 0.35f);
    }

    private static FilteredBall CreateBall(Vector3 position, Vector3 velocity)
    {
        return new FilteredBall
        {
            State = new BallState
            {
                Position3D = position,
                Velocity = velocity,
                Acceleration = Vector3.Zero,
                SpinRadians = Vector2.Zero
            }
        };
    }
}

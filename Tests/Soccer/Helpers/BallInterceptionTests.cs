using System.Numerics;
using Tyr.Common;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.RoleAssignment;
using Tyr.Soccer.Robot;
using Tyr.Vision.Trajectory;
using SslReferee = Tyr.Common.Data.Ssl.Gc.Referee;
using TyrTimer = Tyr.Common.Time.Timer;

namespace Tyr.Tests.Soccer.Helpers;

public class BallInterceptionTests : IDisposable
{
    private readonly BallInterception _ballInterception = new();
    private readonly SoccerContextScope _context = new();

    public BallInterceptionTests()
    {
        ServiceLocator.BallTrajectoryFactory = new BallTrajectoryFactory();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void TryGetInterceptWindow_UsesTouchdownForAirborneBall()
    {
        var ball = CreateBall(
            new Vector3(0f, 0f, 200f),
            new Vector3(1500f, 0f, 800f));
        var trajectory = ServiceLocator.BallTrajectoryFactory.FromState(ball.State);

        var ok = _ballInterception.TryGetInterceptWindow(
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

        var ok = _ballInterception.TryGetInterceptWindow(
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
        var ownPenaltyArea = Rectangle.FromCornerAndSize(new Vector2(4200f, -1800f), 1800f, 3600f);
        var oppPenaltyArea = Rectangle.FromCornerAndSize(new Vector2(-6000f, -1800f), 1800f, 3600f);

        var closeOk = _ballInterception.TryFindPlan(
            ball,
            trajectory,
            new Vector2(800f, 400f),
            Vector2.Zero,
            Angle.Zero,
            VelocityProfile.Mamooli,
            fieldBounds,
            ownPenaltyArea,
            oppPenaltyArea,
            FieldSize.DivisionA.BallRadius,
            75f,
            out var closePlan);

        var farOk = _ballInterception.TryFindPlan(
            ball,
            trajectory,
            new Vector2(800f, 1800f),
            Vector2.Zero,
            Angle.Zero,
            VelocityProfile.Mamooli,
            fieldBounds,
            ownPenaltyArea,
            oppPenaltyArea,
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

    private sealed class SoccerContextScope : IDisposable
    {
        private readonly ContextData? _previous;

        public SoccerContextScope()
        {
            _previous = Context.Data.Value;
            Context.Data.Value = new ContextData
            {
                Color = TeamColor.Blue,
                VisionTime = Timestamp.Zero - Ai.VisionPredictionTime,
                Ball = new FilteredBall
                {
                    Timestamp = Timestamp.Zero,
                    LastVisibleTimestamp = Timestamp.Zero,
                    State = new BallState
                    {
                        Position3D = Vector3.Zero,
                        Velocity = Vector3.Zero,
                    },
                },
                OppRobots = [],
                OwnRobots = [],
                Referee = new State
                {
                    Color = TeamColor.Blue,
                    Gc = new SslReferee { BlueTeamOnPositiveHalf = true },
                },
                Field = FieldSize.DivisionA,
                Timer = new TyrTimer(),
                Knowledge = new Knowledge(),
                RoleAssignment = RoleAssignmentResult.Empty,
            };
        }

        public void Dispose()
        {
            Context.Data.Value = _previous!;
        }
    }
}

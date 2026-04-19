using System.Numerics;
using Tyr.Common;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.RoleAssignment;
using Tyr.Soccer.Skills;
using SslReferee = Tyr.Common.Data.Ssl.Gc.Referee;
using Tyr.Vision.Trajectory;
using TyrTimer = Tyr.Common.Time.Timer;
using RobotRef = Tyr.Soccer.Robot.Robot;

namespace Tyr.Tests.Soccer.Skills;

public sealed class InterceptV2FallbackTests : IDisposable
{
    private readonly ContextData? _previous;

    public InterceptV2FallbackTests()
    {
        _previous = Context.Data.Value;
        ServiceLocator.BallTrajectoryFactory = new BallTrajectoryFactory();

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
                    Acceleration = Vector3.Zero,
                    SpinRadians = Vector2.Zero
                }
            },
            OppRobots = [],
            OwnRobots = [],
            Referee = new State
            {
                Color = TeamColor.Blue,
                GameState = GameState.Running,
                Gc = new SslReferee
                {
                    BlueTeamOnPositiveHalf = true
                }
            },
            Field = FieldSize.DivisionA,
            Timer = new TyrTimer(),
            Knowledge = new Knowledge(),
            RoleAssignment = RoleAssignmentResult.Empty,
        };
    }

    [Fact]
    public void Execute_NoPlanOrImminentImpact_UsesDefaultFallbackPoint()
    {
        var robot = CreateRobot(0, new Vector2(-1200f, 500f), Vector2.Zero);
        SetOwnRobots(robot);
        SetBall(Vector2.Zero, Vector2.Zero);

        var skill = new InterceptV2();
        skill.Execute(robot);
        robot.WaitForNavigationJob();

        var expected = ResolveExpectedFallbackDestination(
            Context.Ball.State.Position.PointOnConnectingLine(Context.Field.OwnGoal(), 500f));

        Assert.Equal(expected, robot.TargetPosition, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void Execute_NoPlanOrImminentImpact_UsesProvidedFallbackPoint()
    {
        var robot = CreateRobot(0, new Vector2(-1200f, 500f), Vector2.Zero);
        SetOwnRobots(robot);
        SetBall(Vector2.Zero, Vector2.Zero);
        var providedFallback = new Vector2(900f, -450f);

        var skill = new InterceptV2
        {
            FallbackPoint = providedFallback
        };

        skill.Execute(robot);
        robot.WaitForNavigationJob();

        var expected = ResolveExpectedFallbackDestination(providedFallback);

        Assert.Equal(expected, robot.TargetPosition, Utils.ApproximatelyEqual);
    }

    public void Dispose()
    {
        Context.Data.Value = _previous!;
    }

    private static RobotRef CreateRobot(int id, Vector2 position, Vector2 velocity)
    {
        return new RobotRef
        {
            Filtered = new FilteredRobot
            {
                Id = new RobotId { Id = (uint)id, Team = TeamColor.Blue },
                Timestamp = Timestamp.Zero,
                Quality = 1.0f,
                State = new RobotState
                {
                    Position = position,
                    Velocity = velocity
                }
            }
        };
    }

    private static void SetOwnRobots(params RobotRef[] robots)
    {
        Context.Data.Value = Context.Data.Value! with
        {
            OwnRobots = new List<RobotRef>(robots)
        };
    }

    private static void SetBall(Vector2 position, Vector2 velocity)
    {
        Context.Data.Value = Context.Data.Value! with
        {
            Ball = new FilteredBall
            {
                Timestamp = Timestamp.Zero,
                LastVisibleTimestamp = Timestamp.Zero,
                State = new BallState
                {
                    Position3D = new Vector3(position, 0f),
                    Velocity = new Vector3(velocity, 0f),
                    Acceleration = Vector3.Zero,
                    SpinRadians = Vector2.Zero
                }
            }
        };
    }

    private static Vector2 ResolveExpectedFallbackDestination(Vector2 rawFallback)
    {
        var ballPosition = Context.Ball.State.Position;
        var ballVelocity = Context.Ball.State.Velocity.Xy();

        return Context.Knowledge.BallReceiving.ClampToLegalDestination(
            rawFallback,
            Context.Field.RectangleWithBoundary,
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            Context.Knowledge.BallReceiving.PenaltyAreaMargin,
            ballPosition,
            ballVelocity);
    }
}

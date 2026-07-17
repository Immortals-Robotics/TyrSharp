using System.Numerics;
using Tyr.Common;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Extensions;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.RoleAssignment;
using Tyr.Soccer.Robot;
using RobotRef = Tyr.Soccer.Robot.Robot;
using SslReferee = Tyr.Common.Data.Ssl.Gc.Referee;
using Tyr.Vision.Trajectory;
using TyrTimer = Tyr.Common.Time.Timer;

namespace Tyr.Tests.Soccer.KnowledgeTests;

public sealed class KnowledgeAttackerCostTests : IDisposable
{
    private readonly ContextData? _previous;
    private readonly PhysicalStatus[] _previousStatusArray;

    public KnowledgeAttackerCostTests()
    {
        _previous = Context.Data.Value;
        ServiceLocator.BallTrajectoryFactory = new BallTrajectoryFactory();

        // Fully-able robots by default, so the ability penalty doesn't skew
        // the pure cost assertions below.
        _previousStatusArray = PhysicalStatus.StatusArray;
        PhysicalStatus.StatusArray = Enumerable.Range(0, CommonConfigs.MaxRobots)
            .Select(_ => new PhysicalStatus { HasDribbler = true, HasDirectKick = true, HasChipKick = true })
            .ToArray();

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
                    Velocity = Vector3.Zero
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
    public void GetAttackerAssignmentCost_UsesReachTimeWhenBallIsSlow()
    {
        var robot = CreateRobot(0, new Vector2(1200f, 400f), Vector2.Zero);
        SetOwnRobots(robot);
        SetGoalkeeper(1);
        SetBall(new Vector3(0f, 0f, 0f), new Vector3(250f, 0f, 0f));

        Context.Knowledge.Update();

        var expected = CalculateReachTimeToCurrentBall(robot);
        var actual = Context.Knowledge.GetAttackerAssignmentCost(robot);

        Assert.InRange(System.Math.Abs(actual.Seconds - expected.Seconds), 0.0, 1e-6);
    }

    [Fact]
    public void GetAttackerAssignmentCost_UsesInterceptionTimeWhenBallIsFast()
    {
        var robot = CreateRobot(0, new Vector2(800f, 1800f), Vector2.Zero);
        SetOwnRobots(robot);
        SetGoalkeeper(1);
        SetBall(new Vector3(-500f, 0f, 0f), new Vector3(1800f, 0f, 0f));

        var hasPlan = Context.Knowledge.BallInterception.TryFindPlan(
            Context.Ball,
            ServiceLocator.BallTrajectoryFactory.FromState(Context.Ball.State),
            robot.Position,
            robot.Velocity,
            (Context.Ball.State.Position - robot.Position).ToAngle(),
            VelocityProfile.Mamooli,
            Context.Field.RectangleWithBoundary,
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            Context.Field.BallRadius,
            robot.CenterToDribbler,
            out var plan);

        Assert.True(hasPlan);

        Context.Knowledge.Update();

        var actual = Context.Knowledge.GetAttackerAssignmentCost(robot);
        var directReach = CalculateReachTimeToCurrentBall(robot);

        Assert.InRange(System.Math.Abs(actual.Seconds - plan.TimeSeconds), 0.0, 1e-3);
        Assert.True(System.Math.Abs(actual.Seconds - directReach.Seconds) > 1e-2);
    }

    [Fact]
    public void GetAttackerAssignmentCost_PenalizesMissingKickerAndDribbler()
    {
        // Two robots at the same spot; robot 1 has no kicker/dribbler.
        PhysicalStatus.StatusArray[1] = new PhysicalStatus();

        var fullyAble = CreateRobot(0, new Vector2(1200f, 400f), Vector2.Zero);
        var limited = CreateRobot(1, new Vector2(1200f, 400f), Vector2.Zero);
        SetOwnRobots(fullyAble, limited);
        SetGoalkeeper(2);
        SetBall(new Vector3(0f, 0f, 0f), new Vector3(250f, 0f, 0f));

        Context.Knowledge.Update();

        var fullyAbleCost = Context.Knowledge.GetAttackerAssignmentCost(fullyAble);
        var limitedCost = Context.Knowledge.GetAttackerAssignmentCost(limited);

        Assert.True(limitedCost > fullyAbleCost);
    }

    public void Dispose()
    {
        Context.Data.Value = _previous!;
        PhysicalStatus.StatusArray = _previousStatusArray;
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

    private static void SetBall(Vector3 position, Vector3 velocity)
    {
        Context.Data.Value = Context.Data.Value! with
        {
            Ball = new FilteredBall
            {
                Timestamp = Timestamp.Zero,
                LastVisibleTimestamp = Timestamp.Zero,
                State = new BallState
                {
                    Position3D = position,
                    Velocity = velocity,
                    Acceleration = Vector3.Zero,
                    SpinRadians = Vector2.Zero
                }
            }
        };
    }

    private static void SetGoalkeeper(uint id)
    {
        var gc = Context.Data.Value!.Referee.Gc;
        var blue = gc.Blue;
        blue.Goalkeeper = id;
        gc.Blue = blue;
    }

    private static DeltaTime CalculateReachTimeToCurrentBall(RobotRef robot)
    {
        var trajectory = TrajectoryBangBang.Make2D(
            robot.Position,
            robot.Velocity,
            Context.Ball.State.Position,
            VelocityProfile.Mamooli);

        return DeltaTime.FromSeconds(trajectory.Duration);
    }
}

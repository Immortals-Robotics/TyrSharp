using System.Numerics;
using Tyr.Common;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Extensions;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Plays;
using Tyr.Soccer.RoleAssignment;
using Tyr.Vision.Trajectory;
using RobotRef = Tyr.Soccer.Robot.Robot;

namespace Tests.Soccer.Plays;

public sealed class StatefulPlayTests : IDisposable
{
    private readonly ContextData? _previousContext;

    public StatefulPlayTests()
    {
        _previousContext = Context.Data.Value;
        ServiceLocator.BallTrajectoryFactory = new BallTrajectoryFactory();
    }

    [Fact]
    public void NormalPlay_DefendingState_UsesDefensiveAttackerAndMarking()
    {
        var ownRobots = CreateOwnRobots(6);
        var opponent = CreateOpponent(1, new Vector2(-2500f, 0f));
        var knowledge = SetupContext(
            gameState: GameState.Running,
            ballPosition: new Vector2(-600f, 0f),
            ownRobots: ownRobots,
            oppRobots: [opponent]);

        // Ensure deterministic threat list for the defending branch.
        knowledge.OpponentThreats.Clear();
        knowledge.OpponentThreats.Add((opponent, 1f));

        var play = new NormalPlay();
        var formation = play.Tick();

        var attacker = Assert.IsType<Tyr.Soccer.Role.Attacker>(
            formation.RequiredRoles.Single(r => r is Tyr.Soccer.Role.Attacker));
        Assert.Equal(Tyr.Soccer.Role.Attacker.PlayMode.Defending, attacker.Mode);
        Assert.False(attacker.AllowPassing);
        Assert.Contains(formation.DesiredRoles, r => r is Tyr.Soccer.Role.Mark);
    }

    [Fact]
    public void NormalPlay_AttackingState_UsesPassingAttackerAndSupporters()
    {
        var ownRobots = CreateOwnRobots(6);
        var knowledge = SetupContext(
            gameState: GameState.Running,
            ballPosition: new Vector2(2000f, 0f),
            ownRobots: ownRobots,
            oppRobots: []);

        Assert.False(knowledge.IsDefending);

        var play = new NormalPlay();
        var formation = play.Tick();

        var attacker = Assert.IsType<Tyr.Soccer.Role.Attacker>(
            formation.RequiredRoles.Single(r => r is Tyr.Soccer.Role.Attacker));
        Assert.Equal(Tyr.Soccer.Role.Attacker.PlayMode.Attacking, attacker.Mode);
        Assert.True(attacker.AllowPassing);
        Assert.Contains(formation.DesiredRoles, r => r is Tyr.Soccer.Role.Supporter);
    }

    [Fact]
    public void Stop_DefendingState_UsesMarking()
    {
        var ownRobots = CreateOwnRobots(6);
        var opponent = CreateOpponent(1, new Vector2(-2500f, 200f));
        var knowledge = SetupContext(
            gameState: GameState.Stop,
            ballPosition: Vector2.Zero,
            ownRobots: ownRobots,
            oppRobots: [opponent]);

        knowledge.OpponentThreats.Clear();
        knowledge.OpponentThreats.Add((opponent, 1f));

        var play = new Stop();
        var formation = play.Tick();

        Assert.Contains(formation.RequiredRoles, r => r is Tyr.Soccer.Role.StopWall);
        Assert.Contains(formation.DesiredRoles, r => r is Tyr.Soccer.Role.Mark);
    }

    public void Dispose()
    {
        Context.Data.Value = _previousContext!;
    }

    private static Knowledge SetupContext(
        GameState gameState,
        Vector2 ballPosition,
        List<RobotRef> ownRobots,
        List<FilteredRobot> oppRobots)
    {
        var knowledge = new Knowledge();

        Context.Data.Value = new ContextData
        {
            Color = TeamColor.Blue,
            VisionTime = Timestamp.Zero,
            Ball = new FilteredBall
            {
                Timestamp = Timestamp.Zero,
                LastVisibleTimestamp = Timestamp.Zero,
                State = new BallState
                {
                    Position3D = ballPosition.Xyz(),
                    Velocity = Vector3.Zero
                }
            },
            OppRobots = oppRobots,
            OwnRobots = ownRobots,
            Referee = new State
            {
                GameState = gameState,
                Color = TeamColor.Blue,
                Gc = new Referee
                {
                    BlueTeamOnPositiveHalf = false
                }
            },
            Field = FieldSize.DivisionB,
            Timer = new Tyr.Common.Time.Timer(),
            Knowledge = knowledge,
            RoleAssignment = RoleAssignmentResult.Empty
        };

        knowledge.Update();
        return knowledge;
    }

    private static List<RobotRef> CreateOwnRobots(int count)
    {
        var result = new List<RobotRef>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(new RobotRef
            {
                Filtered = new FilteredRobot
                {
                    Id = new Tyr.Common.Data.Ssl.RobotId { Id = (uint)i, Team = TeamColor.Blue },
                    Quality = 1f,
                    State = new RobotState
                    {
                        Position = new Vector2(-3000f + i * 400f, -1000f + i * 200f),
                        Velocity = Vector2.Zero
                    }
                }
            });
        }

        return result;
    }

    private static FilteredRobot CreateOpponent(uint id, Vector2 position)
    {
        return new FilteredRobot
        {
            Id = new Tyr.Common.Data.Ssl.RobotId { Id = id, Team = TeamColor.Yellow },
            Quality = 1f,
            Timestamp = Timestamp.Zero,
            State = new RobotState
            {
                Position = position,
                Velocity = Vector2.Zero
            }
        };
    }
}

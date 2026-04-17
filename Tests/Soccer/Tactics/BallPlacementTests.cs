using System.Numerics;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Role;
using Tyr.Soccer.RoleAssignment;
using Tyr.Soccer.Skills;
using Tyr.Soccer.Tactics;
using SslReferee = Tyr.Common.Data.Ssl.Gc.Referee;
using TyrTimer = Tyr.Common.Time.Timer;
using RobotRef = Tyr.Soccer.Robot.Robot;

namespace Tyr.Tests.Soccer.Tactics;

public sealed class BallPlacementTests : IDisposable
{
    private readonly SoccerContextScope _context = new();

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void LongDistance_PersistsCircleBallState_AndEventuallyKicks()
    {
        var ballPosition = Vector2.Zero;
        var designatedPosition = new Vector2(4500f, 0f);
        var receiverPosition = designatedPosition + Vector2.Normalize(designatedPosition - ballPosition) * Context.Field.RobotRadius;

        var placer1 = CreateRobot(1, receiverPosition);
        var placer2 = CreateRobot(2, new Vector2(-120f, 0f));

        _context.SetBall(ballPosition, Vector2.Zero);
        _context.SetOwnRobots([placer1, placer2]);
        _context.SetRoleAssignment(CreateBallPlacementAssignment(placer1, placer2));
        _context.SetReferee(designatedPosition);

        var tactic = new BallPlacement(placer2, placerId: 2);

        Assert.IsType<GoToPoint>(tactic.Tick());

        _context.SetTime(DeltaTime.FromMilliseconds(50));
        Assert.IsType<GoToPoint>(tactic.Tick());

        _context.SetTime(DeltaTime.FromMilliseconds(80));
        var kick = Assert.IsType<CircleKick>(tactic.Tick());
        Assert.Equal(2000f, kick.ShootPower);
        Assert.Equal(0f, kick.ChipPower);
    }

    private static RobotRef CreateRobot(int id, Vector2 position)
    {
        return new RobotRef
        {
            Filtered = new FilteredRobot
            {
                Id = new RobotId { Id = (uint)id, Team = TeamColor.Blue },
                Quality = 1.0f,
                State = new RobotState
                {
                    Position = position
                }
            }
        };
    }

    private static RoleAssignmentResult CreateBallPlacementAssignment(RobotRef placer1Robot, RobotRef placer2Robot)
    {
        var placer1Role = new BallPlacer(1);
        var placer2Role = new BallPlacer(2);

        return new RoleAssignmentResult(
            FilledRoles:
            [
                new FilledRoleAssignment(placer1Robot, placer1Role),
                new FilledRoleAssignment(placer2Robot, placer2Role)
            ],
            UnfilledRoles: [],
            UnassignedRobots: [],
            RoleMapping: new Dictionary<int, IRole?>
            {
                [placer1Robot.Id] = placer1Role,
                [placer2Robot.Id] = placer2Role
            },
            TotalCost: 0.0);
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
                    State = new BallState()
                },
                OppRobots = [],
                OwnRobots = [],
                Referee = new State
                {
                    Color = TeamColor.Blue,
                    GameState = GameState.BallPlacement,
                    Gc = new SslReferee
                    {
                        BlueTeamOnPositiveHalf = true
                    },
                },
                Field = FieldSize.DivisionA,
                Timer = new TyrTimer(),
                Knowledge = new Knowledge(),
                RoleAssignment = RoleAssignmentResult.Empty,
            };
        }

        public void SetBall(Vector2 position, Vector2 velocity)
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

        public void SetOwnRobots(IReadOnlyList<RobotRef> robots)
        {
            Context.Data.Value = Context.Data.Value! with
            {
                OwnRobots = robots.ToList()
            };
        }

        public void SetRoleAssignment(RoleAssignmentResult assignment)
        {
            Context.Data.Value = Context.Data.Value! with
            {
                RoleAssignment = assignment
            };
        }

        public void SetReferee(Vector2 designatedPosition)
        {
            Context.Data.Value = Context.Data.Value! with
            {
                Referee = new State
                {
                    Color = TeamColor.Blue,
                    GameState = GameState.BallPlacement,
                    Gc = new SslReferee
                    {
                        BlueTeamOnPositiveHalf = true,
                        DesignatedPosition = designatedPosition
                    },
                }
            };
        }

        public void SetTime(DeltaTime time)
        {
            Context.Data.Value = Context.Data.Value! with
            {
                VisionTime = Timestamp.Zero + time - Ai.VisionPredictionTime
            };
        }

        public void Dispose()
        {
            Context.Data.Value = _previous!;
        }
    }
}

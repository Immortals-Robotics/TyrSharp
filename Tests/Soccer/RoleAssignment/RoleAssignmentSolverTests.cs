using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer;
using Tyr.Soccer.Plays;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Role;
using Tyr.Soccer.RoleAssignment;
using Tyr.Soccer.Skills;
using Tyr.Soccer.Tactics;
using SslReferee = Tyr.Common.Data.Ssl.Gc.Referee;
using TyrTimer = Tyr.Common.Time.Timer;
using RobotRef = Tyr.Soccer.Robot.Robot;

namespace Tyr.Tests.Soccer.RoleAssignment;

public sealed class RoleAssignmentSolverTests : IDisposable
{
    private readonly RoleAssignmentSolver _solver = new(requiredRoleUnfilledPenaltySeconds: 30.0);
    private readonly SoccerContextScope _context = new();

    public RoleAssignmentSolverTests()
    {
        _context.SetOwnRobots([]);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void Solve_ChoosesGlobalOptimum_WhenGreedyWouldFail()
    {
        var robots = CreateRobots(0, 1);
        _context.SetOwnRobots(robots);
        var striker = new TestRole("Striker", 1, 10f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 10.0, [1] = 11.0 })
        {
        };
        var helper = new TestRole("Helper", 1, 1f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 0.1, [1] = 100.0 })
        {
        };

        var result = _solver.Solve(new Formation
        {
            RequiredRoles = [striker, helper]
        }, previousRoleMapping: new Dictionary<int, IRole?>());

        Assert.Contains(result.FilledRoles, x => x.Robot.Id == 1 && Equals(x.Role, striker));
        Assert.Contains(result.FilledRoles, x => x.Robot.Id == 0 && Equals(x.Role, helper));
    }

    [Fact]
    public void Solve_FillsRequiredRoles_WhenEnoughRobotsAreAvailable()
    {
        var robots = CreateRobots(0, 1, 2);
        _context.SetOwnRobots(robots);
        var keeper = new TestRole("Keeper", 1, 100f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 0.1, [1] = 10.0, [2] = 10.0 })
        {
        };
        var defender = new TestRole("Defender", 1, 50f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 10.0, [1] = 0.2, [2] = 10.0 })
        {
        };

        var result = _solver.Solve(new Formation
        {
            RequiredRoles = [keeper, defender]
        }, previousRoleMapping: new Dictionary<int, IRole?>());

        Assert.Equal(2, result.FilledRoles.Count);
        Assert.Empty(result.UnfilledRoles);
        Assert.Single(result.UnassignedRobots);
    }

    [Fact]
    public void Solve_AllowsDesiredRolesToRemainUnfilled_WhenTheyAreTooExpensive()
    {
        var robots = CreateRobots(0);
        _context.SetOwnRobots(robots);
        var required = new TestRole("Required", 1, 10f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 0.2 })
        {
        };
        var desired = new TestRole("Desired", 1, 1f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 100.0 })
        {
        };

        var result = _solver.Solve(new Formation
        {
            RequiredRoles = [required],
            DesiredRoles = [desired]
        }, previousRoleMapping: new Dictionary<int, IRole?>());

        Assert.Single(result.FilledRoles);
        Assert.Contains(result.UnfilledRoles, x => Equals(x.Role, desired) && !x.IsRequired);
    }

    [Fact]
    public void Solve_AssignsDesiredRoles_WhenRobotsAreAvailable()
    {
        var robots = CreateRobots(0, 1);
        _context.SetOwnRobots(robots);
        var required = new TestRole("Required", 1, 10f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 0.2, [1] = 1.0 })
        {
        };
        var desired = new TestRole("Desired", 1, 1f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 1.0, [1] = 0.3 })
        {
        };

        var result = _solver.Solve(new Formation
        {
            RequiredRoles = [required],
            DesiredRoles = [desired]
        }, previousRoleMapping: new Dictionary<int, IRole?>());

        Assert.Equal(2, result.FilledRoles.Count);
        Assert.Empty(result.UnfilledRoles);
        Assert.Empty(result.UnassignedRobots);
        Assert.Contains(result.FilledRoles, x => x.Robot.Id == 0 && Equals(x.Role, required));
        Assert.Contains(result.FilledRoles, x => x.Robot.Id == 1 && Equals(x.Role, desired));
    }

    [Fact]
    public void Solve_LeavesExtraRobotsUnassigned_WhenThereAreMoreRobotsThanRoles()
    {
        var robots = CreateRobots(0, 1, 2);
        _context.SetOwnRobots(robots);
        var role = new TestRole("Solo", 1, 1f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 3.0, [1] = 2.0, [2] = 1.0 })
        {
        };

        var result = _solver.Solve(new Formation
        {
            RequiredRoles = [role]
        }, previousRoleMapping: new Dictionary<int, IRole?>());

        Assert.Single(result.FilledRoles);
        Assert.Equal(2, result.UnassignedRobots.Count);
        Assert.Equal(2, result.FilledRoles[0].Robot.Id);
    }

    [Fact]
    public void Solve_LeavesRolesUnfilled_WhenThereAreFewerRobotsThanRequiredRoles()
    {
        var robots = CreateRobots(0);
        _context.SetOwnRobots(robots);
        var first = new TestRole("First", 1, 5f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 0.1 })
        {
        };
        var second = new TestRole("Second", 1, 5f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 0.2 })
        {
        };

        var result = _solver.Solve(new Formation
        {
            RequiredRoles = [first, second]
        }, previousRoleMapping: new Dictionary<int, IRole?>());

        Assert.Single(result.FilledRoles);
        Assert.Single(result.UnfilledRoles);
        Assert.True(result.UnfilledRoles[0].IsRequired);
    }

    [Fact]
    public void Solve_PrefersKeepingPreviousAssignment_WhenStickinessCoversTheGap()
    {
        var robots = CreateRobots(0, 1);
        _context.SetOwnRobots(robots);
        var role = new TestRole("Sticky", 1, 1f, DeltaTime.FromMilliseconds(50), new Dictionary<int, double> { [0] = 1.00, [1] = 0.96 })
        {
        };

        var result = _solver.Solve(new Formation
        {
            RequiredRoles = [role]
        }, previousRoleMapping: new Dictionary<int, IRole?> { [0] = role });

        Assert.Equal(0, result.FilledRoles[0].Robot.Id);
    }

    [Fact]
    public void Solve_SwitchesAssignment_WhenAlternativeIsBetterBeyondStickiness()
    {
        var robots = CreateRobots(0, 1);
        _context.SetOwnRobots(robots);
        var role = new TestRole("Sticky", 1, 1f, DeltaTime.FromMilliseconds(50), new Dictionary<int, double> { [0] = 1.02, [1] = 0.90 })
        {
        };

        var result = _solver.Solve(new Formation
        {
            RequiredRoles = [role]
        }, previousRoleMapping: new Dictionary<int, IRole?> { [0] = role });

        Assert.Equal(1, result.FilledRoles[0].Robot.Id);
    }

    [Fact]
    public void Solve_DistinguishesRoleIdentity_WhenApplyingStickiness()
    {
        var robots = CreateRobots(0, 1);
        _context.SetOwnRobots(robots);
        var newRole = new TestRole("Defender", 2, 1f, DeltaTime.FromMilliseconds(50), new Dictionary<int, double> { [0] = 1.00, [1] = 0.97 })
        {
        };

        var result = _solver.Solve(new Formation
        {
            RequiredRoles = [newRole]
        }, previousRoleMapping: new Dictionary<int, IRole?> { [0] = new TestRole("Defender", 1, 1f, DeltaTime.FromMilliseconds(50), new Dictionary<int, double>()) });

        Assert.Equal(1, result.FilledRoles[0].Robot.Id);
    }

    [Fact]
    public void Solve_ExcludesRefereeGoalkeeperFromAssignmentAndForcesGoalieOnlyRole()
    {
        var robots = CreateRobots(0, 1);
        _context.SetOwnRobots(robots);
        _context.SetGoalkeeper(0);
        var goalieRole = new TestRole("GoalieOnly", 1, 100f, DeltaTime.Zero, new Dictionary<int, double>(), isGoalie: true)
        {
        };
        var fieldRole = new TestRole("Field", 1, 10f, DeltaTime.Zero, new Dictionary<int, double> { [0] = 0.01, [1] = 1.0 })
        {
        };

        var result = _solver.Solve(new Formation
        {
            RequiredRoles = [goalieRole, fieldRole]
        }, previousRoleMapping: new Dictionary<int, IRole?>());

        Assert.Contains(result.FilledRoles, x => x.Robot.Id == 0 && Equals(x.Role, goalieRole));
        Assert.Contains(result.FilledRoles, x => x.Robot.Id == 1 && Equals(x.Role, fieldRole));
        Assert.DoesNotContain(result.UnassignedRobots, robot => robot.Id == 0);
    }

    private static IReadOnlyList<RobotRef> CreateRobots(params int[] ids)
    {
        return ids.Select(id => new RobotRef
        {
            Filtered = new FilteredRobot
            {
                Id = new RobotId { Id = (uint)id, Team = TeamColor.Blue },
                Quality = 1.0f,
                State = new RobotState()
            }
        }).ToArray();
    }

    private sealed record TestRole(
        string Name,
        int Variant,
        float Importance,
        DeltaTime StickinessBonus,
        IReadOnlyDictionary<int, double> Costs,
        bool isGoalie = false) : IRole
    {
        public bool IsGoalie { get; } = isGoalie;

        public DeltaTime CostFor(RobotRef robot) => DeltaTime.FromSeconds(Costs[robot.Id]);

        public ITactic CreateTactic(RobotRef robot) => new NullTactic(robot);
    }

    private sealed class NullTactic(RobotRef robot) : ITactic
    {
        public RobotRef Robot => robot;

        public ISkill? Tick() => null;
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
                    Gc = new SslReferee
                    {
                        BlueTeamOnPositiveHalf = true,
                        Blue = new global::Tyr.Common.Data.Ssl.Gc.TeamInfo { Goalkeeper = 99 }
                    },
                },
                Field = FieldSize.DivisionA,
                Timer = new TyrTimer(),
                Knowledge = new Knowledge(),
                RoleAssignment = RoleAssignmentResult.Empty,
            };
        }

        public void SetOwnRobots(IReadOnlyList<RobotRef> robots)
        {
            Context.Data.Value = Context.Data.Value! with
            {
                OwnRobots = robots.ToList()
            };
        }

        public void SetGoalkeeper(int robotId)
        {
            var referee = Context.Data.Value!.Referee;
            var gc = referee.Gc;

            if (Context.Color == TeamColor.Blue)
            {
                var blue = gc.Blue;
                blue.Goalkeeper = (uint)robotId;
                gc.Blue = blue;
            }
            else
            {
                var yellow = gc.Yellow;
                yellow.Goalkeeper = (uint)robotId;
                gc.Yellow = yellow;
            }

            Context.Data.Value = Context.Data.Value! with
            {
                Referee = referee with { Gc = gc }
            };
        }

        public void Dispose()
        {
            Context.Data.Value = _previous!;
        }
    }
}

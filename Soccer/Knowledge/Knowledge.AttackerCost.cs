using Tyr.Common;
using Tyr.Common.Config;
using Tyr.Common.Extensions;
using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;
using RobotRef = Tyr.Soccer.Robot.Robot;

namespace Tyr.Soccer.Knowledge;

public partial class Knowledge
{
    [ConfigEntry("Ball-speed threshold for switching attacker assignment from static-ball reach to moving-ball interception [mm/s]")]
    private static float AttackerStaticBallMaxSpeedMmPerS { get; set; } = 300f;

    private readonly Dictionary<int, DeltaTime> _attackerAssignmentCosts = [];

    public DeltaTime GetAttackerAssignmentCost(RobotRef robot)
    {
        if (IsOurGoalie(robot))
        {
            return DeltaTime.MaxValue;
        }

        if (_attackerAssignmentCosts.TryGetValue(robot.Id, out var cached))
        {
            return cached;
        }

        return CalculateAttackerAssignmentCost(robot);
    }

    private void UpdateAttackerAssignmentCosts()
    {
        _attackerAssignmentCosts.Clear();

        foreach (var robot in Context.OwnRobots)
        {
            if (!robot.Seen || IsOurGoalie(robot))
            {
                continue;
            }

            _attackerAssignmentCosts[robot.Id] = CalculateAttackerAssignmentCost(robot);
        }
    }

    private static bool IsOurGoalie(RobotRef robot)
    {
        return robot.Id == (int)Context.Referee.OurInfo().Goalkeeper;
    }

    private DeltaTime CalculateAttackerAssignmentCost(RobotRef robot)
    {
        var reachTimeToCurrentBall = CalculateReachTimeToCurrentBall(robot);
        var ballSpeed = Context.Ball.State.Velocity.Xy().Length();

        if (ballSpeed < AttackerStaticBallMaxSpeedMmPerS)
        {
            return reachTimeToCurrentBall;
        }

        var trajectory = ServiceLocator.BallTrajectoryFactory.FromState(Context.Ball.State);
        var toBall = Context.Ball.State.Position - robot.Position;
        var fallbackAngle = toBall.LengthSquared() > 1f ? toBall.ToAngle() : robot.Angle;
        var hasPlan = BallInterception.TryFindPlan(
            Context.Ball,
            trajectory,
            robot.Position,
            robot.Velocity,
            fallbackAngle,
            VelocityProfile.Mamooli,
            Context.Field.RectangleWithBoundary,
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            Context.Field.BallRadius,
            robot.CenterToDribbler,
            out var plan);

        if (hasPlan)
        {
            return DeltaTime.FromSeconds(plan.TimeSeconds);
        }

        return reachTimeToCurrentBall;
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

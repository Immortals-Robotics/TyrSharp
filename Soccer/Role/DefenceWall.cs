using System.Numerics;
using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

/// <param name="Kickoff">Their kickoff: the wall uses the fixed kickoff limit angle instead of the ball-x curve.</param>
public record DefenceWall(bool Kickoff = false) : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.DefenceWall(robot, Kickoff);
    }

    public float Importance => 0.8f;

    public DeltaTime CostFor(Robot.Robot robot)
    {
        // Simple distance-based cost to the expected wall position
        var distance = 700.0f + Context.RobotRadius;
        var targetPos = Context.Ball.State.Position.CircleAroundPoint(
                Context.Ball.State.Position.AngleWith(Context.Field.OwnGoal()),
                distance);

        var trajectory = TrajectoryBangBang.Make2D(robot.Position, robot.Velocity,
            targetPos, VelocityProfile.Mamooli);
        return DeltaTime.FromSeconds(trajectory.Duration);
    }
}

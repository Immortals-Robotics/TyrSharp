using System.Numerics;
using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record DefenceWall : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.DefenceWall(robot);
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

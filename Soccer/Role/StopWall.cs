using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record StopWall : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.StopWall(robot);
    }

    public float Importance => 0.8f;
    public DeltaTime CostFor(Robot.Robot robot)
    {
        var trajectory = TrajectoryBangBang.Make2D(robot.Position, robot.Velocity,
            Tactics.StopWall.GetTargetPosition(), VelocityProfile.Mamooli);
        return DeltaTime.FromSeconds(trajectory.Duration);
    }
}

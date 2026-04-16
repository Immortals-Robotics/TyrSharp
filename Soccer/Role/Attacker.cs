using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record Attacker : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.Attacker(robot);
    }

    public float Importance => 1f;
    public DeltaTime CostFor(Robot.Robot robot)
    {
        var trajectory = TrajectoryBangBang.Make2D(robot.Position, robot.Velocity,
            Context.Ball.State.Position, VelocityProfile.Mamooli);
        return DeltaTime.FromSeconds(trajectory.Duration);
    }
}

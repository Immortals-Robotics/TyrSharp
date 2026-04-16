using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record PenaltyKicker : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.PenaltyUsShootout(robot);
    }

    public float Importance => 100f;

    public DeltaTime CostFor(Robot.Robot robot)
    {
        var trajectory = TrajectoryBangBang.Make2D(robot.Position, robot.Velocity,
            Context.Ball.State.Position, VelocityProfile.Mamooli);
        return DeltaTime.FromSeconds(trajectory.Duration);
    }
}

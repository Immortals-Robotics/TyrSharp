using Tyr.Common.Time;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record Supporter : IRole
{
    public required Zone Zone { get; init; }

    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.Supporter(robot) { Zone = Zone };
    }

    public float Importance => 0.5f;
    public DeltaTime CostFor(Robot.Robot robot)
    {
        var trajectory = TrajectoryBangBang.Make2D(robot.Position, robot.Velocity,
            Zone.Rect.Center, VelocityProfile.Mamooli);
        return DeltaTime.FromSeconds(trajectory.Duration);
    }
}

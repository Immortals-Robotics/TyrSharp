using System.Numerics;
using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record WaitForBall(Vector2 Target) : IRole
{
    public float Importance => 1f;

    public Tactics.ITactic CreateTactic(Robot.Robot robot) => new Tactics.WaitForBall(robot, Target);

    public DeltaTime CostFor(Robot.Robot robot)
    {
        var trajectory = TrajectoryBangBang.Make2D(robot.Position, robot.Velocity,
            Target, VelocityProfile.Mamooli);
        return DeltaTime.FromSeconds(trajectory.Duration);
    }
}

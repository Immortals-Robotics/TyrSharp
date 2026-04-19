using System.Numerics;
using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record Defender(int DefId) : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.Defender(robot, DefId);
    }

    public float Importance => 10f;

    public DeltaTime CostFor(Robot.Robot robot)
    {
        var staticPos = Tactics.Defender.CalculateStaticPos(DefId);
        var trajectory = TrajectoryBangBang.Make2D(robot.Position, robot.Velocity,
            staticPos, VelocityProfile.Mamooli);
        return DeltaTime.FromSeconds(trajectory.Duration);
    }
}
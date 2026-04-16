using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record ThrowInReceiver(float RandomParam) : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.ThrowInReceiver(robot, RandomParam);
    }

    public float Importance => 0.8f;

    public DeltaTime CostFor(Robot.Robot robot)
    {
        var targetPos = Tactics.ThrowInReceiver.CalculateStaticPos(RandomParam);
        var trajectory = TrajectoryBangBang.Make2D(robot.Position, robot.Velocity,
            targetPos, VelocityProfile.Mamooli);
        return DeltaTime.FromSeconds(trajectory.Duration);
    }
}

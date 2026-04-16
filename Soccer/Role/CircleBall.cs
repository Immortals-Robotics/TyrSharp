using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record CircleBall : IRole
{
    public float Importance => 1f;

    public required Vector2 TargetPosition { get; init; }
    public required bool CanKick { get; init; }

    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.CircleBall(robot)
        {
            TargetAngle = Angle.FromVector(Context.Ball.State.Position - TargetPosition),
            ShootPower = CanKick ? 3000f : 0f,
        };
    }

    public DeltaTime CostFor(Robot.Robot robot)
    {
        var trajectory = TrajectoryBangBang.Make2D(robot.Position, robot.Velocity,
            Context.Ball.State.Position, VelocityProfile.Mamooli);
        return DeltaTime.FromSeconds(trajectory.Duration);
    }
}

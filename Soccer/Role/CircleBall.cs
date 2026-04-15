using System.Numerics;
using Tyr.Common.Math;

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

    public float CostFor(Robot.Robot robot)
    {
        return Vector2.Distance(robot.Position, Context.Ball.State.Position);
    }
}

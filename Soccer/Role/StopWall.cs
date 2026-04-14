using System.Numerics;

namespace Tyr.Soccer.Role;

public record StopWall : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.StopWall(robot);
    }

    public float Importance => 0.8f;

    public float CostFor(Robot.Robot robot)
    {
        return Vector2.Distance(robot.Position, Tactics.StopWall.GetTargetPosition());
    }
}

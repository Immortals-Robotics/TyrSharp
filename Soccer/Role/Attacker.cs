using System.Numerics;

namespace Tyr.Soccer.Role;

public record Attacker : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.Attacker(robot);
    }

    public float Importance => 1f;

    public float CostFor(Robot.Robot robot)
    {
        return Vector2.Distance(robot.Position, Context.Ball.State.Position);
    }
}

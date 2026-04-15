using System.Numerics;

namespace Tyr.Soccer.Role;

public record Def(int DefId) : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.Def(robot, DefId);
    }

    public float Importance => 80f;

    public float CostFor(Robot.Robot robot)
    {
        var staticPos = Tactics.Def.CalculateStaticPos(DefId);
        return Vector2.Distance(robot.Position, staticPos);
    }
}

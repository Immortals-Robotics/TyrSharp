using System.Numerics;
using Tyr.Common.Vision.Data;

namespace Tyr.Soccer.Role;

public record class Attacker : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.Attacker(robot);
    }

    public float Importance => 1f;
    
    public float CostFor(RobotState robot)
    {
        return Vector2.Distance(robot.Position, Context.Ball.State.Position);
    }
}
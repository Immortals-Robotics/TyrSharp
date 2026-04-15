using System.Numerics;

namespace Tyr.Soccer.Role;

public record Goalie : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.Goalie(robot);
    }

    public float Importance => 100f;

    public float CostFor(Robot.Robot robot)
    {
        if (robot.Id == (int)Context.Referee.OurInfo().Goalkeeper)
        {
            return -100000f;
        }
        
        return float.MaxValue;
    }
}

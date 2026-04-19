using Tyr.Common.Time;

namespace Tyr.Soccer.Role;

public record Goalie : IRole
{
    public bool IsGoalie => true;

    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.Goalie(robot);
    }

    public float Importance => 100f;

    public DeltaTime CostFor(Robot.Robot robot)
    {
        return robot.Id == (int)Context.Referee.OurInfo().Goalkeeper
            ? DeltaTime.Zero
            : DeltaTime.MaxValue;
    }
}

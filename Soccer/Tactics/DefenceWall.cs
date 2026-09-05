using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

public class DefenceWall(Robot.Robot robot, bool kickoff = false) : ITactic
{
    public Robot.Robot Robot => robot;

    public ISkill? Tick() => new Skills.DefenceWall { Kickoff = kickoff };
}
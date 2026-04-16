using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

public class DefenceWall(Robot.Robot robot) : ITactic
{
    public Robot.Robot Robot => robot;

    public ISkill? Tick() => new Skills.DefenceWall();
}
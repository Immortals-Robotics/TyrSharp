using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

public interface ITactic
{
    public void Reset();
    public ISkill Tick(Robot.Robot robot);
}
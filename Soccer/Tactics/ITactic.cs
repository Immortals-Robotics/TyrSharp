using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

public interface ITactic
{
    Robot.Robot Robot { get; }
    
    public ISkill? Tick();
}
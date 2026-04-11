using SoccerRobot = Tyr.Soccer.Robot.Robot;

namespace Tyr.Soccer.Skills;

public interface ISkill
{
    public void Execute(SoccerRobot robot);
}

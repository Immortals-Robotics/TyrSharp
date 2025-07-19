namespace Tyr.Soccer.Skills;

public class Halt : ISkill
{
    public void Execute(Robot.Robot robot)
    {
        if (robot.CurrentMotion.Length() > 100.0f)
        {
            robot.FullBrake();
        }
        else
        {
            robot.Halt();
        }
    }
}
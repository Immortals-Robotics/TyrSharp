using System.Numerics;
using Tyr.Common.Math;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

public class DiveSkill : ISkill
{
    public required Vector2 Target { get; init; }
    public required VelocityProfile VelocityProfile { get; init; }
    public NavigationFlags NavigationFlags { get; init; }
    
    public float Kick { get; init; }
    public float Chip { get; init; }

    public void Execute(Robot.Robot robot)
    {
        robot.Face(Context.Ball.State.Position);
        robot.Navigate(Target, VelocityProfile, NavigationFlags);
        
        if (Kick > 0) robot.Kick = Kick;
        if (Chip > 0) robot.Chip = Chip;
    }
}

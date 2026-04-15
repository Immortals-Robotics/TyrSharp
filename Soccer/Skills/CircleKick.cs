using Tyr.Common.Math;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

public class CircleKick : ISkill
{
    public required Angle TargetAngle { get; init; }
    public required float ShootPower { get; init; }
    public required float ChipPower { get; init; }

    public void Execute(Robot.Robot robot)
    {
        robot.TargetAngle = TargetAngle + Angle.FromDeg(180.0f);
        robot.Navigate(Context.Ball.State.Position, VelocityProfile.Aroom);
        robot.Shoot = ShootPower;
        robot.Chip = ChipPower;
    }
}

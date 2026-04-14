using System.Numerics;
using Tyr.Common.Math;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

public class GoToPoint : ISkill
{
    public required Vector2 Target { get; init; }
    public VelocityProfile VelocityProfile { get; init; } = VelocityProfile.Mamooli;

    public NavigationFlags NavigationFlags { get; init; } = NavigationFlags.None;

    public Vector2? LookAt { get; init; }
    public Angle? Angle { get; init; }

    public void Execute(Robot.Robot robot)
    {
        if (LookAt.HasValue)
            robot.Face(LookAt.Value);
        else if (Angle.HasValue)
            robot.TargetAngle = Angle.Value;

        robot.Navigate(Target, VelocityProfile, NavigationFlags);
    }
}

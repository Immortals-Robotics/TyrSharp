using System.Numerics;
using Tyr.Common.Config;
using Tyr.Soccer.Robot;
using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

[Configurable]
public partial class StopWall(Robot.Robot robot) : ITactic
{
    [ConfigEntry] private static float StopDistance { get; set; } = 650f;

    public Robot.Robot Robot => robot;

    public static Vector2 GetTargetPosition()
    {
        return Context.Ball.State.Position.PointOnConnectingLine(Context.Field.OwnGoal(), StopDistance);
    }

    public ISkill Tick()
    {
        return new GoToPoint()
        {
            Target = GetTargetPosition(),
            LookAt = Context.Ball.State.Position,
            VelocityProfile = VelocityProfile.Aroom,
        };
    }
}

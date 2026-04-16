using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Soccer.Robot;
using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

public class PenaltyWait(Robot.Robot robot, int index) : ITactic
{
    public Robot.Robot Robot { get; } = robot;
    public int Index { get; } = index;

    public ISkill Tick()
    {
        var targetPos = CalculateStaticPos(Index);
        Robot.Face(Context.Field.OppGoal());
        return new GoToPoint
        {
            Target = targetPos,
            VelocityProfile = VelocityProfile.Mamooli,
            NavigationFlags = NavigationFlags.BallObstacle | NavigationFlags.NoOwnPenaltyArea
        };
    }

    public static Vector2 CalculateStaticPos(int index)
    {
        // Position robots in our half, spaced out along Y
        float x = Context.SideSign * 4000;
        float y = (index - 1) * 1000 - 1000;
        return new Vector2(x, y);
    }
}

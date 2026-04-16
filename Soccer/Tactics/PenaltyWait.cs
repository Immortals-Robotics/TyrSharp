using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Soccer.Robot;
using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

public class PenaltyWait(Robot.Robot robot, int index, bool isOurPenalty) : ITactic
{
    public Robot.Robot Robot { get; } = robot;
    public int Index { get; } = index;

    public ISkill Tick()
    {
        var targetPos = CalculateStaticPos(Index, isOurPenalty);
        Robot.Face(Context.Field.OppGoal());
        return new GoToPoint
        {
            Target = targetPos,
            VelocityProfile = VelocityProfile.Mamooli,
            NavigationFlags = NavigationFlags.BallObstacle | NavigationFlags.NoOwnPenaltyArea
        };
    }

    public static Vector2 CalculateStaticPos(int index, bool isOurPenalty)
    {
        // Position robots in our half, spaced out along Y
        // If it's our penalty, we can be closer to the center (x=4000)
        // If it's their penalty, we should stay further back (x=5000)
        float x = Context.SideSign * (isOurPenalty ? 4000 : 5000);
        float y = (index - 1) * 800 - 2000;
        return new Vector2(x, y);
    }
}

using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Soccer.Robot;
using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

public class ThrowInReceiver(Robot.Robot robot, float randomParam) : ITactic
{
    public Robot.Robot Robot => robot;

    public ISkill Tick()
    {
        var ballPos = Context.Ball.State.Position;
        var oppGoalX = Context.Field.OppGoal().X;
        
        Vector2 targetPos;
        if (randomParam < 0.5f)
        {
            targetPos = ballPos.PointOnConnectingLine(
                new Vector2(oppGoalX, Math.Sign(-ballPos.X) * 2000.0f), 350f);
        }
        else
        {
            targetPos = ballPos.PointOnConnectingLine(
                new Vector2(oppGoalX, Math.Sign(ballPos.X) * 2000.0f), 350f);
        }

        Robot.Face(Context.Field.OppGoal());
        return new GoToPoint
        {
            Target = targetPos,
            VelocityProfile = VelocityProfile.Mamooli,
            NavigationFlags = NavigationFlags.BallMediumObstacle
        };
    }

    public static Vector2 CalculateStaticPos(float randomParam)
    {
        var ballPos = Context.Ball.State.Position;
        var oppGoalX = Context.Field.OppGoal().X;
        
        if (randomParam < 0.5f)
        {
            return ballPos.PointOnConnectingLine(
                new Vector2(oppGoalX, Math.Sign(-ballPos.X) * 2000.0f), 350f);
        }
        else
        {
            return ballPos.PointOnConnectingLine(
                new Vector2(oppGoalX, Math.Sign(ballPos.X) * 2000.0f), 350f);
        }
    }
}

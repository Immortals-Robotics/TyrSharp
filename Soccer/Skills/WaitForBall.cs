using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Common.Math.Shapes;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

public sealed class WaitForBall : ISkill
{
    public Vector2 StaticPosition { get; set; }

    public void Execute(Robot.Robot robot)
    {
        var receiverPos = StaticPosition;
        var ballVelocity = Context.Ball.State.Velocity.Xy();

        if (ballVelocity.Length() >= 100f)
        {
            var ballLine = Line.FromTwoPoints(Context.Ball.State.Position,
                Context.Ball.State.Position + Vector2.Normalize(ballVelocity) * 1000f);
            receiverPos = ballLine.ClosestPoint(robot.Position);
        }

        robot.Navigate(receiverPos, VelocityProfile.Mamooli);
        robot.TargetAngle = receiverPos.AngleWith(Context.Ball.State.Position);
    }
}

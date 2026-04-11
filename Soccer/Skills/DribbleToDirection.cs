using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

public sealed class DribbleToDirection : ISkill
{
    public Angle Direction { get; set; }

    public void Execute(Robot.Robot robot)
    {
        var targetDir = -Direction.ToUnitVec();
        var targetAngle = targetDir.ToAngle();
        var ballPos = Context.Ball.State.Position;

        var ballDist = Vector2.Distance(ballPos, robot.Position);
        var robotFront = robot.Angle.ToUnitVec();
        var robotToBallRaw = ballPos - robot.Position;
        var ballBehind = ballDist < Context.RobotRadius * 1.5f && Vector2.Dot(robotFront, robotToBallRaw) < 0f;
        var ballInside = ballDist < Context.RobotRadius * 0.8f;

        var effectiveBallPos = (ballBehind || ballInside)
            ? robot.Position + robotFront * Context.RobotRadius
            : ballPos;

        var toBallAngle = (effectiveBallPos - robot.Position).ToAngle();
        var angleToTarget = (targetAngle - toBallAngle).Deg;
        var heading = SkillMath.CalculateSteeringHeading(toBallAngle, angleToTarget);
        var target = SkillMath.CalculateWrapTarget(effectiveBallPos, heading, angleToTarget);

        robot.Navigate(target, VelocityProfile.Mamooli, NavigationFlags.NoExtraMargin | NavigationFlags.NoBreak);
        robot.TargetAngle = heading;
        robot.Dribbler = 1f;
    }
}

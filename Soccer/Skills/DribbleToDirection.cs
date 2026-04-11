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
        var angleToTarget = (targetAngle - toBallAngle).DegNormalized;
        var heading = CalculateSteeringHeading(toBallAngle, angleToTarget);
        var target = CalculateWrapTarget(effectiveBallPos, heading, angleToTarget);

        robot.Navigate(target, VelocityProfile.Mamooli, NavigationFlags.NoExtraMargin | NavigationFlags.NoBreak);
        robot.TargetAngle = heading;
    }

    private static Angle CalculateSteeringHeading(Angle toBallAngle, float angleToTarget)
    {
        const float maxSteeringAngle = 15f;
        var angleOffset = MathF.Abs(angleToTarget) > maxSteeringAngle
            ? MathF.CopySign(maxSteeringAngle, angleToTarget)
            : angleToTarget;

        return toBallAngle + Angle.FromDeg(angleOffset);
    }

    private static Vector2 CalculateWrapTarget(Vector2 effectiveBallPos, Angle heading, float angleToTarget)
    {
        var headingDir = heading.ToUnitVec();
        var turnSign = angleToTarget > 0f ? 1f : -1f;
        var behind = -headingDir * (Context.RobotRadius + 20f);
        var sideOffset = (heading - Angle.FromDeg(turnSign * 90f)).ToUnitVec() * (Context.RobotRadius * 0.5f);
        return effectiveBallPos + behind + sideOffset;
    }
}
using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

public sealed class TurnAndShoot : ISkill
{
    public Angle Angle { get; set; }
    public float Kick { get; set; }
    public float Chip { get; set; }

    private readonly KickBall _kickBall = new();

    public void Execute(Robot.Robot robot)
    {
        var shootDir = -Angle.ToUnitVec();
        var shootAngle = shootDir.ToAngle();
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
        var angleToTarget = (shootAngle - toBallAngle).Deg;
        var angleError = MathF.Abs(angleToTarget);

        if (angleError < 8f)
        {
            _kickBall.Angle = Angle;
            _kickBall.Kick = Kick;
            _kickBall.Chip = Chip;
            _kickBall.IsGoalkeeper = false;
            _kickBall.Execute(robot);
            return;
        }

        var heading = SkillMath.CalculateSteeringHeading(toBallAngle, angleToTarget);
        var target = SkillMath.CalculateWrapTarget(effectiveBallPos, heading, angleToTarget);

        robot.Navigate(target, VelocityProfile.Kharaki, NavigationFlags.NoExtraMargin | NavigationFlags.NoBreak);
        robot.TargetAngle = heading;
    }
}

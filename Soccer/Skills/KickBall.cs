using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Soccer.Helpers;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

[Configurable]
public sealed partial class KickBall : ISkill
{
    [ConfigEntry] private static DeltaTime PredictionTime { get; set; } = DeltaTime.FromMilliseconds(200);
    [ConfigEntry] private static float MaxAngleError { get; set; } = 3;

    public Angle Angle { get; set; }
    public float Kick { get; set; }
    public float Chip { get; set; }
    public bool IsGoalkeeper { get; set; }

    public void Execute(Robot.Robot robot)
    {
        var maxForwardOffset = IsGoalkeeper ? 200f : 300f;
        var navigationFlags = GetNavigationFlags(robot);
        var ball = Context.Knowledge.BallPrediction.PredictBall(PredictionTime);

        var toTarget = Angle.ToUnitVec();
        var robotToBall = ball.Position - robot.Position;
        var toBall = Utils.ApproximatelyZero(robotToBall.LengthSquared())
            ? Vector2.Zero
            : Vector2.Normalize(robotToBall);

        var ballTargetDot = Vector2.Dot(toTarget, toBall);
        var rScaleFactor = 2f * MathF.Pow((1f - ballTargetDot) * 0.5f, 0.5f) - 1f;
        var radius = MathF.Min((IsGoalkeeper ? 70f : 50f) + 120f * rScaleFactor, 500f);
        var behindBallDistance = Context.RobotRadius + radius;

        var behindBallPos = ball.Position.CircleAroundPoint(Angle + Angle.FromDeg(180f), behindBallDistance);
        var alignmentFactor = Math.Clamp(ballTargetDot, 0f, 1f);
        var axialDistanceToBall = Vector2.Dot(toTarget, robotToBall);
        var distanceProgress = 1f - Math.Clamp(axialDistanceToBall / behindBallDistance, 0f, 1f);
        var distanceFactor = SmoothStep01(distanceProgress);
        var forwardOffset = maxForwardOffset * alignmentFactor * distanceFactor;
        var finalPos = behindBallPos + toTarget * forwardOffset;

        robot.DynamicBallObstacleRadius = radius;
        var profile = Vector2.Distance(robot.Position, ball.Position) < 30f
            ? VelocityProfile.Kharaki
            : VelocityProfile.Mamooli;
        robot.Navigate(finalPos, profile, navigationFlags);

        if (IsGoalkeeper)
        {
            robot.Face(ball.Position);
        }
        else
        {
            var kickDirection = ball.Position - behindBallPos;
            robot.TargetAngle = Utils.ApproximatelyZero(kickDirection.LengthSquared())
                ? robot.Angle
                : kickDirection.ToAngle();
        }

        BallControl.TryEnableDribblerWhenBallNearKicker(robot, ball.Position);

        if (MathF.Abs((robot.Angle - Angle).DegNormalized) < MaxAngleError)
        {
            robot.Shoot = Kick;
            robot.Chip = Chip;
        }
        else
        {
            robot.Shoot = 0;
            robot.Chip = 0;
        }
    }

    private static float SmoothStep01(float value)
    {
        var t = Math.Clamp(value, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private NavigationFlags GetNavigationFlags(Robot.Robot robot)
    {
        var navigationFlags = NavigationFlags.DynamicBallObstacle;

        if (Context.Referee.Running() && Vector2.Distance(robot.Position, Context.Ball.State.Position) < 500f)
        {
            navigationFlags |= NavigationFlags.NoExtraMargin;
            navigationFlags |= NavigationFlags.NoBreak;
        }

        if (IsGoalkeeper)
        {
            navigationFlags |= NavigationFlags.NoOwnPenaltyArea;
        }

        return navigationFlags;
    }
}

using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Soccer.Helpers;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

/// <summary>
/// A high-precision, low-latency skill for capturing a stationary or slow-moving ball.
/// </summary>
[Configurable]
public sealed partial class CaptureBall : ISkill
{
    [ConfigEntry("Dribbler Force used while capturing the ball")]
    private static float CaptureDribblerForce { get; set; } = 2f;

    [ConfigEntry("Angle tolerance within which the robot starts charging inward [deg]")]
    private static float RoughFocusAngleDeg { get; set; } = 15f;

    [ConfigEntry("Distance margin in front of the ball during approach [mm]")]
    private static float ApproachMarginMm { get; set; } = 100f;

    [ConfigEntry("Distance margin behind the ball center during final capture [mm]")]
    private static float CaptureMarginMm { get; set; } = -20f;

    [ConfigEntry("Dribbler speed used while capturing the ball")]
    private static float CaptureDribblerSpeed { get; set; } = 2f;

    public void Execute(Robot.Robot robot)
    {
        var ballPosition = Context.Ball.State.Position;
        var toBall = ballPosition - robot.Position;

        // 1. Instant Braking on Contact
        if (robot.HasBallContact)
        {
            // Lock current pose and spin up dribbler to secure the ball
            robot.Navigate(robot.Position, VelocityProfile.Mamooli, NavigationFlags.NoBallObstacle);
            robot.TargetAngle = robot.Angle;
            robot.SetDribbler(CaptureDribblerSpeed, CaptureDribblerForce);
            return;
        }

        // 2. Angle Forgiveness
        var targetAngle = toBall.ToAngle();
        var angleDiff = MathF.Abs((targetAngle - robot.Angle).DegNormalized);
        var isRoughlyFocused = angleDiff < RoughFocusAngleDeg;

        // 3. Soft Charge logic
        // If not focused, aim for a point in front of the ball to give space for rotation.
        // If focused, aim past the ball center to "charge" into it.
        float currentMargin = isRoughlyFocused ? CaptureMarginMm : ApproachMarginMm;

        var direction = Vector2.Normalize(toBall);
        var destination =
            ballPosition - direction * (robot.CenterToDribbler + Context.Field.BallRadius + currentMargin);

        // Clamping to legal field bounds
        destination = BallReceiving.ClampToLegalDestination(
            destination,
            Context.Field.RectangleWithBoundary,
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            BallReceiving.PenaltyAreaMargin,
            ballPosition,
            Context.Ball.State.Velocity.Xy());

        // 4. Low-Latency Execution
        robot.Navigate(destination, VelocityProfile.Mamooli, NavigationFlags.NoBallObstacle);
        robot.TargetAngle = targetAngle;
        robot.SetDribbler(CaptureDribblerSpeed, CaptureDribblerForce);
    }
}

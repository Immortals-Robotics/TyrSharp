using System.Numerics;
using Tyr.Common;
using Tyr.Common.Config;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

[Configurable]
public sealed partial class InterceptBall : ISkill
{
    private const float ReceiveDribblerSpeed = 2f;
    private const float ReceiveDribblerForce = 5f;
    private const float DribblerActivationTimeSeconds = 0.2f;

    [ConfigEntry("Default fallback distance from ball toward our goal when no interception solution is available [mm]")]
    private static float DefaultFallbackDistanceMm { get; set; } = 500f;

    public Vector2? FallbackPoint { get; set; }

    private BallInterception.InterceptPlan? _lastPlan;

    public void Execute(Robot.Robot robot)
    {
        var ballPosition = Context.Ball.State.Position;
        var ballVelocity = Context.Ball.State.Velocity.Xy();

        var trajectory = ServiceLocator.BallTrajectoryFactory.FromState(Context.Ball.State);
        var centerToDribbler = robot.CenterToDribbler;

        var hasPlan = Context.Knowledge.BallInterception.TryFindPlan(
            Context.Ball,
            trajectory,
            robot.Position,
            robot.Velocity,
            robot.TargetAngle,
            VelocityProfile.Mamooli,
            Context.Field.RectangleWithBoundary,
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            Context.Field.BallRadius,
            centerToDribbler,
            out var plan,
            _lastPlan);

        if (!hasPlan)
        {
            var fallbackPoint = ResolveFallbackDestination(ballPosition, ballVelocity);
            robot.Navigate(fallbackPoint, VelocityProfile.Mamooli, NavigationFlags.NoBallObstacle);
            robot.TargetAngle = fallbackPoint.AngleWith(ballPosition);
            robot.SetDribbler(0f, 0f);
            _lastPlan = null;
            return;
        }

        var destination = Context.Knowledge.BallReceiving.ClampToLegalDestination(
            plan.CenterDestination,
            Context.Field.RectangleWithBoundary,
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            Context.Knowledge.BallReceiving.PenaltyAreaMargin,
            plan.BallState.Position,
            plan.BallState.Velocity.Xy());

        robot.Navigate(destination, VelocityProfile.Mamooli, NavigationFlags.NoBallObstacle);
        robot.TargetAngle = plan.FacingAngle;

        var dribblerActive = plan.TimeSeconds < DribblerActivationTimeSeconds;
        robot.SetDribbler(
            dribblerActive ? ReceiveDribblerSpeed : 0f,
            dribblerActive ? ReceiveDribblerForce : 0f);

        _lastPlan = plan;
    }

    private Vector2 ResolveFallbackDestination(Vector2 ballPosition, Vector2 ballVelocity)
    {
        var rawFallback = FallbackPoint ?? ballPosition.PointOnConnectingLine(Context.Field.OwnGoal(), DefaultFallbackDistanceMm);

        return Context.Knowledge.BallReceiving.ClampToLegalDestination(
            rawFallback,
            Context.Field.RectangleWithBoundary,
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            Context.Knowledge.BallReceiving.PenaltyAreaMargin,
            ballPosition,
            ballVelocity);
    }
}

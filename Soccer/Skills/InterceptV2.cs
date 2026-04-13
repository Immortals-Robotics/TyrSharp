using System.Numerics;
using Tyr.Common;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Soccer.Helpers;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

[Configurable]
public sealed partial class InterceptV2 : ISkill
{
    private const float ReceiveDribblerForce = 1f;

    [ConfigEntry("Dribbler speed used while receiving a moving ball")]
    private static float ReceiveDribblerSpeed { get; set; } = 2f;

    [ConfigEntry("Ball arrival time below which the bot forces blocking on the ball path [s]")]
    private static float ImminentImpactTimeSeconds { get; set; } = 0.3f;

    [ConfigEntry("Distance from robot kicker to ball path below which the bot forces blocking [mm]")]
    private static float ImminentImpactDistanceMm { get; set; } = 150f;

    [ConfigEntry("Time until ball impact below which the dribbler is activated [s]")]
    private static float DribblerActivationTimeSeconds { get; set; } = 0.2f;

    private bool _hasTargetAngle;
    private Angle _lastTargetAngle;
    private bool _hasDestination;
    private Vector2 _lastDestination;
    private BallInterception.InterceptPlan? _lastPlan;

    public void Execute(Robot.Robot robot)
    {
        var ballPosition = Context.Ball.State.Position;
        var ballVelocity = Context.Ball.State.Velocity.Xy();

        var trajectory = ServiceLocator.BallTrajectoryFactory.FromState(Context.Ball.State);
        var centerToDribbler = robot.CenterToDribbler;
        var previousTargetAngle = _hasTargetAngle
            ? _lastTargetAngle
            : robot.Position.AngleWith(ballPosition);

        // Forced Line Blocking (Safety Fallback)
        var ballPath = Line.FromPointAndAngle(ballPosition, ballVelocity.ToAngle());
        var kickerPos = Tyr.Common.Math.Shapes.Robot.GetKickerCenterPos(robot.Position, robot.TargetAngle,
            centerToDribbler + Context.Field.BallRadius);
        var distToPath = ballPath.Distance(kickerPos);
        var ballSpeed = ballVelocity.Length();
        var ballArrivalDist = Vector2.Distance(ballPosition, ballPath.ClosestPoint(kickerPos));
        var ballArrivalTime = ballSpeed > 1e-3f ? ballArrivalDist / ballSpeed : float.PositiveInfinity;

        if (distToPath < ImminentImpactDistanceMm && ballArrivalTime < ImminentImpactTimeSeconds)
        {
            var interceptPoint = ballPath.ClosestPoint(kickerPos);
            var facingAngle = (-ballVelocity).ToAngle();
            var destination = BallReceiving.GetCenterDestination(interceptPoint, facingAngle, centerToDribbler,
                Context.Field.BallRadius);

            destination = BallReceiving.ClampToLegalDestination(
                destination,
                Context.Field.RectangleWithBoundary,
                Context.Field.OwnPenaltyArea(),
                Context.Field.OppPenaltyArea(),
                BallReceiving.PenaltyAreaMargin);

            robot.Navigate(destination, VelocityProfile.Mamooli, NavigationFlags.NoBallObstacle);
            robot.TargetAngle = facingAngle;
            var dribblerActive = ballArrivalTime < DribblerActivationTimeSeconds;
            robot.SetDribbler(
                dribblerActive ? ReceiveDribblerSpeed : 0f,
                dribblerActive ? ReceiveDribblerForce : 0f);

            _hasTargetAngle = true;
            _lastTargetAngle = facingAngle;
            _hasDestination = true;
            _lastDestination = destination;
            DrawDebug(ballPosition, interceptPoint, destination, facingAngle, ballArrivalTime, "FORCED");
            return;
        }

        var hasPlan = BallInterception.TryFindPlan(
            Context.Ball,
            trajectory,
            robot.Position,
            robot.Velocity,
            previousTargetAngle,
            VelocityProfile.Mamooli,
            Context.Field.RectangleWithBoundary,
            Context.Field.BallRadius,
            centerToDribbler,
            out var plan,
            _lastPlan);

        if (!hasPlan)
        {
            if (_hasDestination)
            {
                robot.Navigate(_lastDestination, VelocityProfile.Mamooli, NavigationFlags.NoBallObstacle);
                robot.TargetAngle = _lastTargetAngle;
                robot.SetDribbler(0f, 0f); // No impact imminent if plan lost
            }
            else
            {
                robot.Halt();
                Reset();
            }

            return;
        }

        var finalDestination = BallReceiving.ClampToLegalDestination(
            plan.CenterDestination,
            Context.Field.RectangleWithBoundary,
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            BallReceiving.PenaltyAreaMargin);

        if (!float.IsFinite(finalDestination.X) || !float.IsFinite(finalDestination.Y))
        {
            if (_hasDestination)
            {
                finalDestination = _lastDestination;
            }
            else
            {
                Reset();
                robot.Halt();
                return;
            }
        }

        robot.Navigate(finalDestination, VelocityProfile.Mamooli, NavigationFlags.NoBallObstacle);
        robot.TargetAngle = plan.FacingAngle;
        var shouldActivateDribbler = plan.TimeSeconds < DribblerActivationTimeSeconds;
        robot.SetDribbler(
            shouldActivateDribbler ? ReceiveDribblerSpeed : 0f,
            shouldActivateDribbler ? ReceiveDribblerForce : 0f);

        _hasTargetAngle = true;
        _lastTargetAngle = plan.FacingAngle;
        _hasDestination = true;
        _lastDestination = finalDestination;
        _lastPlan = plan;

        DrawDebug(ballPosition, plan.BallState.Position, finalDestination, plan.FacingAngle, plan.TimeSeconds,
            "SOLVED");
    }

    private void Reset()
    {
        _hasTargetAngle = false;
        _hasDestination = false;
        _lastPlan = null;
    }

    private static void DrawDebug(
        Vector2 ballPosition,
        Vector2 interceptPoint,
        Vector2 destination,
        Angle targetAngle,
        float timeSeconds,
        string mode)
    {
        Draw.DrawCircle(ballPosition, 60f, Color.Red, options: Options.Outline(20f));
        Draw.DrawCircle(interceptPoint, 40f, Color.Green400, options: Options.Outline(20f));
        Draw.DrawCircle(destination, 60f, Color.Orange400, options: Options.Outline(20f));
        Draw.DrawLineSegment(destination, destination + targetAngle.ToUnitVec() * 250f, Color.Black);
        Draw.DrawText(
            $"{mode} T={timeSeconds:F2}s",
            interceptPoint + new Vector2(0f, 180f),
            180f,
            Color.Black);
    }
}

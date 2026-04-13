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

    private readonly record struct ImminentImpact(Vector2 InterceptPoint, float BallArrivalTime);

    public readonly record struct ForcedReceiveExecution(
        Vector2 BallPosition,
        Vector2 InterceptPoint,
        Vector2 Destination,
        Angle FacingAngle,
        float BallArrivalTime);

    [ConfigEntry("Dribbler speed used while receiving a moving ball")]
    private static float ReceiveDribblerSpeed { get; set; } = 2f;

    [ConfigEntry("Ball arrival time below which the bot forces blocking on the ball path [s]")]
    private static float ImminentImpactTimeSeconds { get; set; } = 0.5f;

    [ConfigEntry("Distance from robot kicker to ball path below which the bot forces blocking [mm]")]
    private static float ImminentImpactDistanceMm { get; set; } = 250f;

    [ConfigEntry("Time until ball impact below which the dribbler is activated [s]")]
    private static float DribblerActivationTimeSeconds { get; set; } = 0.2f;

    public static bool HasImminentImpact(Robot.Robot robot, Angle? targetAngle = null) =>
        TryGetImminentImpact(robot, out _, targetAngle);

    public static bool IsBallApproachingRobot(Robot.Robot robot)
    {
        return BallReceiving.IsBallMovingTowardsPoint(
            Context.Ball.State.Position,
            Context.Ball.State.Velocity.Xy(),
            robot.Position);
    }

    public static void SetReceiveDribbler(Robot.Robot robot, bool enabled)
    {
        robot.SetDribbler(
            enabled ? ReceiveDribblerSpeed : 0f,
            enabled ? ReceiveDribblerForce : 0f);
    }

    public static bool TryExecuteForcedReceive(Robot.Robot robot, out ForcedReceiveExecution execution,
        Angle? targetAngle = null)
    {
        execution = default;

        if (!TryGetImminentImpact(robot, out var imminentImpact, targetAngle))
        {
            return false;
        }

        var ballPosition = Context.Ball.State.Position;
        var ballVelocity = Context.Ball.State.Velocity.Xy();
        var centerToDribbler = robot.CenterToDribbler;
        var interceptPoint = imminentImpact.InterceptPoint;
        var ballArrivalTime = imminentImpact.BallArrivalTime;
        var facingAngle = (-ballVelocity).ToAngle();
        var destination = BallReceiving.GetCenterDestination(
            interceptPoint,
            facingAngle,
            centerToDribbler,
            Context.Field.BallRadius);

        destination = BallReceiving.ClampToLegalDestination(
            destination,
            Context.Field.RectangleWithBoundary,
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            BallReceiving.PenaltyAreaMargin);

        robot.Navigate(destination, VelocityProfile.Mamooli, NavigationFlags.NoBallObstacle);
        robot.TargetAngle = facingAngle;
        SetReceiveDribbler(robot, true);

        execution = new ForcedReceiveExecution(
            ballPosition,
            interceptPoint,
            destination,
            facingAngle,
            ballArrivalTime);

        return true;
    }

    private bool _hasTargetAngle;
    private Angle _lastTargetAngle;
    private bool _hasDestination;
    private Vector2 _lastDestination;
    private BallInterception.InterceptPlan? _lastPlan;

    public void Execute(Robot.Robot robot)
    {
        var ballPosition = Context.Ball.State.Position;
        var ballVelocity = Context.Ball.State.Velocity.Xy();
        var navigationFlags = GetInterceptionNavigationFlags(robot, ballPosition, ballVelocity);

        var trajectory = ServiceLocator.BallTrajectoryFactory.FromState(Context.Ball.State);
        var centerToDribbler = robot.CenterToDribbler;
        var previousTargetAngle = _hasTargetAngle
            ? _lastTargetAngle
            : robot.Position.AngleWith(ballPosition);

        // Forced Line Blocking (Safety Fallback)
        if (TryExecuteForcedReceive(robot, out var forcedReceive))
        {
            _hasTargetAngle = true;
            _lastTargetAngle = forcedReceive.FacingAngle;
            _hasDestination = true;
            _lastDestination = forcedReceive.Destination;
            DrawDebug(
                forcedReceive.BallPosition,
                forcedReceive.InterceptPoint,
                forcedReceive.Destination,
                forcedReceive.FacingAngle,
                forcedReceive.BallArrivalTime,
                "FORCED");
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
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            Context.Field.BallRadius,
            centerToDribbler,
            out var plan,
            _lastPlan);

        if (!hasPlan)
        {
            if (_hasDestination)
            {
                robot.Navigate(_lastDestination, VelocityProfile.Mamooli, navigationFlags);
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

        robot.Navigate(finalDestination, VelocityProfile.Mamooli, navigationFlags);
        robot.TargetAngle = plan.FacingAngle;
        var shouldActivateDribbler = plan.TimeSeconds < DribblerActivationTimeSeconds;
        SetReceiveDribbler(robot, shouldActivateDribbler);

        _hasTargetAngle = true;
        _lastTargetAngle = plan.FacingAngle;
        _hasDestination = true;
        _lastDestination = finalDestination;
        _lastPlan = plan;

        DrawDebug(ballPosition, plan.BallState.Position, finalDestination, plan.FacingAngle, plan.TimeSeconds,
            "SOLVED");
    }

    private static bool TryGetImminentImpact(Robot.Robot robot, out ImminentImpact imminentImpact,
        Angle? targetAngle = null)
    {
        var ballPosition = Context.Ball.State.Position;
        var ballVelocity = Context.Ball.State.Velocity.Xy();
        var ballPath = Line.FromPointAndAngle(ballPosition, ballVelocity.ToAngle());
        var kickerPos = Tyr.Common.Math.Shapes.Robot.GetKickerCenterPos(
            robot.Position,
            targetAngle ?? robot.TargetAngle,
            robot.CenterToDribbler + Context.Field.BallRadius);

        var interceptPoint = ballPath.ClosestPoint(kickerPos);
        var distToPath = ballPath.Distance(kickerPos);
        var ballSpeed = ballVelocity.Length();
        var ballArrivalDist = Vector2.Distance(ballPosition, interceptPoint);
        var ballArrivalTime = ballSpeed > 1e-3f ? ballArrivalDist / ballSpeed : float.PositiveInfinity;

        imminentImpact = new ImminentImpact(interceptPoint, ballArrivalTime);
        return distToPath < ImminentImpactDistanceMm && ballArrivalTime < ImminentImpactTimeSeconds;
    }

    private static NavigationFlags GetInterceptionNavigationFlags(
        Robot.Robot robot,
        Vector2 ballPosition,
        Vector2 ballVelocity)
    {
        return BallReceiving.IsBallMovingTowardsPoint(ballPosition, ballVelocity, robot.Position)
            ? NavigationFlags.NoBallObstacle
            : NavigationFlags.BallObstacle;
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
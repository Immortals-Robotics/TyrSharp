using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Helpers;

[Configurable]
public static partial class BallInterception
{
    [ConfigEntry("Lower planar ball-speed threshold for interception [mm/s]")]
    private static float MinBallSpeedLowerMmPerS { get; set; } = 100f;

    [ConfigEntry("Upper planar ball-speed threshold for interception [mm/s]")]
    private static float MinBallSpeedUpperMmPerS { get; set; } = 250f;

    [ConfigEntry("Search horizon hard cap for moving-ball interception [s]")]
    private static float MaxSearchTimeSeconds { get; set; } = 5f;

    [ConfigEntry("Sampling step for moving-ball interception search [s]")]
    private static float SearchStepSeconds { get; set; } = 0.05f;

    [ConfigEntry("Sampling step when finding the valid search horizon [s]")]
    private static float HorizonStepSeconds { get; set; } = 0.05f;

    [ConfigEntry("Height above which interception waits for the next touchdown [mm]")]
    private static float MaxReceiveHeightMm { get; set; } = 120f;

    [ConfigEntry("Maximum acceptable absolute slack time between robot and ball [s]")]
    private static float MaxSlackTimeSeconds { get; set; } = 0.35f;

    [ConfigEntry("Minimum spare time buffer to delay movement [s]")]
    private static float SpareTimeBufferSeconds { get; set; } = 0.3f;

    [ConfigEntry("Objective bonus bias toward earlier interceptions [s]")]
    private static float EarlyInterceptionBonus { get; set; } = 2.0f;

    [ConfigEntry("Time window over which the early interception bonus fades out [s]")]
    private static float EarlyInterceptionWindowSeconds { get; set; } = 2.0f;

    [ConfigEntry("Hysteresis bonus for sticking to the previous plan [s]")]
    private static float HysteresisBonusSeconds { get; set; } = 0.15f;

    [ConfigEntry("Distance within which the previous plan's hysteresis bonus applies [mm]")]
    private static float HysteresisDistanceMm { get; set; } = 150f;

    private static bool _ballMoving;

    public readonly record struct InterceptWindow(float StartTimeSeconds, float EndTimeSeconds);

    public readonly record struct InterceptPlan(
        float TimeSeconds,
        float Objective,
        float SlackTimeSeconds,
        BallState BallState,
        Vector2 CenterDestination,
        Angle FacingAngle)
    {
        public float AbsSlackTimeSeconds => MathF.Abs(SlackTimeSeconds);
    }

    public static bool TryGetInterceptWindow(
        FilteredBall ball,
        IBallTrajectory trajectory,
        Rectangle fieldBounds,
        out InterceptWindow window)
    {
        window = default;

        UpdateBallMoving(ball.State.Velocity.Xy().LengthSquared());

        if (!_ballMoving)
        {
            return false;
        }

        var startTime = 0f;
        if (ball.State.IsChipped &&
            (ball.State.Position3D.Z > MaxReceiveHeightMm || ball.State.Velocity.Z > 0f))
        {
            var touchdown = ball.GetNextTouchdown();
            if (!touchdown.HasValue)
            {
                return false;
            }

            startTime = (float)touchdown.Value.TimeUntilTouchdown.Seconds;
        }

        if (startTime > MaxSearchTimeSeconds)
        {
            return false;
        }

        var startState = trajectory.GetState(DeltaTime.FromSeconds(startTime));
        if (!IsStateUsable(startState, fieldBounds))
        {
            return false;
        }

        var endTime = startTime;
        for (var t = startTime; t <= MaxSearchTimeSeconds; t += HorizonStepSeconds)
        {
            var state = trajectory.GetState(DeltaTime.FromSeconds(t));
            if (!IsStateUsable(state, fieldBounds))
            {
                break;
            }

            endTime = t;
        }

        if (endTime < startTime)
        {
            return false;
        }

        window = new InterceptWindow(startTime, endTime);
        return true;
    }

    public static bool TryFindPlan(
        FilteredBall ball,
        IBallTrajectory trajectory,
        Vector2 robotPosition,
        Vector2 robotMotion,
        Angle fallbackAngle,
        VelocityProfile profile,
        Rectangle fieldBounds,
        Rectangle ownPenaltyArea,
        Rectangle oppPenaltyArea,
        float ballRadius,
        float centerToDribbler,
        out InterceptPlan plan,
        InterceptPlan? previousPlan = null)
    {
        plan = default;

        if (!TryGetInterceptWindow(ball, trajectory, fieldBounds, out var window))
        {
            return false;
        }

        InterceptPlan? best = null;

        for (var t = window.StartTimeSeconds; t <= window.EndTimeSeconds; t += SearchStepSeconds)
        {
            var candidate = Evaluate(
                trajectory,
                t,
                robotPosition,
                robotMotion,
                fallbackAngle,
                profile,
                fieldBounds,
                ownPenaltyArea,
                oppPenaltyArea,
                ballRadius,
                centerToDribbler,
                previousPlan);

            if (candidate.SlackTimeSeconds < 0)
            {
                continue;
            }

            if (best == null || candidate.Objective < best.Value.Objective)
            {
                best = candidate;
            }
        }

        // Final check for the end of the window in case the loop stepped over it
        var finalCandidate = Evaluate(
            trajectory,
            window.EndTimeSeconds,
            robotPosition,
            robotMotion,
            fallbackAngle,
            profile,
            fieldBounds,
            ownPenaltyArea,
            oppPenaltyArea,
            ballRadius,
            centerToDribbler,
            previousPlan);

        if (finalCandidate.SlackTimeSeconds >= 0)
        {
            if (best == null || finalCandidate.Objective < best.Value.Objective)
            {
                best = finalCandidate;
            }
        }

        if (best == null || best.Value.AbsSlackTimeSeconds > MaxSlackTimeSeconds)
        {
            return false;
        }

        plan = best.Value;
        return true;
    }

    private static InterceptPlan Evaluate(
        IBallTrajectory trajectory,
        float timeSeconds,
        Vector2 robotPosition,
        Vector2 robotMotion,
        Angle fallbackAngle,
        VelocityProfile profile,
        Rectangle fieldBounds,
        Rectangle ownPenaltyArea,
        Rectangle oppPenaltyArea,
        float ballRadius,
        float centerToDribbler,
        InterceptPlan? previousPlan)
    {
        var state = trajectory.GetState(DeltaTime.FromSeconds(timeSeconds));
        var facingAngle = BallReceiving.CalculateFacingAngle(state.Velocity.Xy(), fallbackAngle);
        var rawDestination = BallReceiving.GetCenterDestination(state.Position, facingAngle, centerToDribbler, ballRadius);
        var destination = BallReceiving.ClampToLegalDestination(
            rawDestination,
            fieldBounds,
            ownPenaltyArea,
            oppPenaltyArea,
            BallReceiving.PenaltyAreaMargin,
            state.Position,
            state.Velocity.Xy());
        var reachTime = TrajectoryBangBang.Make2D(robotPosition, robotMotion, destination, profile).Duration;
        var slackTime = timeSeconds - reachTime;
        
        // Penalize plans that don't have enough spare time (wait time at destination)
        var spareTimePenalty = MathF.Max(0f, SpareTimeBufferSeconds - slackTime);
        var objective = MathF.Abs(slackTime) + spareTimePenalty - CalculateEarlyInterceptionBonus(timeSeconds);

        if (previousPlan.HasValue && Vector2.DistanceSquared(destination, previousPlan.Value.CenterDestination) < HysteresisDistanceMm * HysteresisDistanceMm)
        {
            objective -= HysteresisBonusSeconds;
        }

        return new InterceptPlan(
            timeSeconds,
            objective,
            slackTime,
            state,
            destination,
            facingAngle);
    }

    private static bool IsStateUsable(BallState state, Rectangle fieldBounds)
    {
        return fieldBounds.Inside(state.Position);
    }

    private static void UpdateBallMoving(float ballSpeedSquared)
    {
        var lowerSquared = MinBallSpeedLowerMmPerS * MinBallSpeedLowerMmPerS;
        var upperSquared = MinBallSpeedUpperMmPerS * MinBallSpeedUpperMmPerS;

        if (_ballMoving)
        {
            _ballMoving = ballSpeedSquared >= lowerSquared;
        }
        else
        {
            _ballMoving = ballSpeedSquared >= upperSquared;
        }
    }

    private static float CalculateEarlyInterceptionBonus(float reachTimeSeconds)
    {
        if (reachTimeSeconds >= EarlyInterceptionWindowSeconds || EarlyInterceptionWindowSeconds <= 0f)
        {
            return 0f;
        }

        return (1f - reachTimeSeconds / EarlyInterceptionWindowSeconds) * EarlyInterceptionBonus;
    }
}

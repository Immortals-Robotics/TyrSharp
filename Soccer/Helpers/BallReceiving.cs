using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;

namespace Tyr.Soccer.Helpers;

[Configurable]
public static partial class BallReceiving
{
    private const float LegalSeparationEpsilonMm = 1f;

    [ConfigEntry("Maximum expected vision latency for receive-point back projection [s]")]
    private static float ExpectedVisionDeviationSeconds { get; set; } = 0.1f;

    [ConfigEntry(
        "If the preferred receive point is further than this from the current ball path, keep the preferred point [mm]")]
    private static float MaxProjectionDistanceMm { get; set; } = 1000f;

    [ConfigEntry("Margin to keep between a receive destination and a penalty area [mm]")]
    private static float PenaltyAreaMarginMm { get; set; } = 110f;

    [ConfigEntry("Distance from receive point to ball where the receive orientation is locked [mm]")]
    private static float OrientationLockDistanceMm { get; set; } = 200f;

    [ConfigEntry("Below this planar ball speed the current orientation is kept for receiving [mm/s]")]
    private static float FacingFallbackSpeedMmPerS { get; set; } = 50f;

    public static float OrientationLockDistance => OrientationLockDistanceMm;
    public static float PenaltyAreaMargin => PenaltyAreaMarginMm;

    public readonly record struct ReceiveProjection(Vector2 Point, bool UsedBackProjection);

    public readonly record struct ReceiveTarget(
        Vector2 ReceivePoint,
        Vector2 Destination,
        Angle FacingAngle,
        bool UsedBackProjection);

    public static Angle CalculateFacingAngle(Vector2 ballVelocity, Angle fallbackAngle)
    {
        return ballVelocity.LengthSquared() < FacingFallbackSpeedMmPerS * FacingFallbackSpeedMmPerS
            ? fallbackAngle
            : (-ballVelocity).ToAngle();
    }

    public static bool IsBallMovingTowardsPoint(Vector2 ballPosition, Vector2 ballVelocity, Vector2 point)
    {
        if (Utils.ApproximatelyZero(ballVelocity.LengthSquared()))
        {
            return false;
        }

        var toPoint = point - ballPosition;
        return Vector2.Dot(ballVelocity, toPoint) > 0f;
    }

    public static float CalculateBackProjectionDistance(IBallTrajectory trajectory)
    {
        return trajectory.GetTravelDistance(DeltaTime.FromSeconds(ExpectedVisionDeviationSeconds));
    }

    public static Vector2 GetKickerReceivePoint(Vector2 robotPosition, Angle targetAngle,
        float centerToDribblerWithBall)
    {
        return Tyr.Common.Math.Shapes.Robot.GetKickerCenterPos(robotPosition, targetAngle, centerToDribblerWithBall);
    }

    public static bool IsProjectionReachable(IBallTrajectory trajectory, Vector2 point)
    {
        return Vector2.Distance(trajectory.ClosestRollingPoint(point), point) <= MaxProjectionDistanceMm;
    }

    public static ReceiveProjection ProjectReceivePoint(
        IBallTrajectory trajectory,
        Vector2 ballPosition,
        Vector2 ballVelocity,
        float backProjectionDistance,
        Vector2 preferredReceivePoint)
    {
        if (Utils.ApproximatelyZero(ballVelocity.LengthSquared()))
        {
            return new ReceiveProjection(preferredReceivePoint, false);
        }

        var forwardPoint = trajectory.ClosestRollingPoint(preferredReceivePoint);

        if (backProjectionDistance <= 0f)
        {
            return new ReceiveProjection(forwardPoint, false);
        }

        var direction = Vector2.Normalize(ballVelocity);
        var backwardSegment = new LineSegment
        {
            Start = ballPosition,
            End = ballPosition - direction * backProjectionDistance
        };

        var backwardPoint = backwardSegment.ClosestPoint(preferredReceivePoint);
        var backwardDistanceSquared = Vector2.DistanceSquared(backwardPoint, preferredReceivePoint);
        var forwardDistanceSquared = Vector2.DistanceSquared(forwardPoint, preferredReceivePoint);

        var projection = backwardDistanceSquared < forwardDistanceSquared
            ? new ReceiveProjection(backwardPoint, true)
            : new ReceiveProjection(forwardPoint, false);

        var reachablePoint = trajectory.ClosestRollingPoint(projection.Point);
        return Vector2.Distance(reachablePoint, projection.Point) > MaxProjectionDistanceMm
            ? new ReceiveProjection(preferredReceivePoint, false)
            : projection;
    }

    public static Vector2 ClampReceivePoint(
        Vector2 receivePoint,
        Rectangle fieldBounds,
        Rectangle ownPenaltyArea,
        Rectangle oppPenaltyArea,
        float ballRadius)
    {
        var effectiveFieldBounds = Shrink(fieldBounds, ballRadius);
        var clamped = ClampInside(receivePoint, effectiveFieldBounds);

        if (ownPenaltyArea.Inside(clamped, ballRadius))
        {
            clamped = ownPenaltyArea.NearestOutside(clamped, ballRadius + LegalSeparationEpsilonMm);
        }

        if (oppPenaltyArea.Inside(clamped, ballRadius))
        {
            clamped = oppPenaltyArea.NearestOutside(clamped, ballRadius + LegalSeparationEpsilonMm);
        }

        return ClampInside(clamped, effectiveFieldBounds);
    }

    public static Vector2 GetCenterDestination(
        Vector2 kickerReceivePoint,
        Angle facingAngle,
        float centerToDribbler,
        float ballRadius)
    {
        var offset = centerToDribbler + ballRadius;
        return kickerReceivePoint - facingAngle.ToUnitVec() * offset;
    }

    public static Vector2 ClampToLegalDestination(
        Vector2 destination,
        Rectangle fieldBounds,
        Rectangle ownPenaltyArea,
        Rectangle oppPenaltyArea,
        float penaltyMargin,
        Vector2? ballPosition = null,
        Vector2? ballVelocity = null)
    {
        var clamped = ClampInside(destination, fieldBounds);

        clamped = ClampOutsidePenaltyArea(clamped, ownPenaltyArea, penaltyMargin, ballPosition, ballVelocity);
        clamped = ClampOutsidePenaltyArea(clamped, oppPenaltyArea, penaltyMargin, ballPosition, ballVelocity);

        return ClampInside(clamped, fieldBounds);
    }

    public static ReceiveTarget ResolveReceiveTarget(
        IBallTrajectory trajectory,
        Vector2 currentBallPosition,
        Vector2 currentBallVelocity,
        Vector2 preferredReceivePoint,
        float centerToDribbler,
        Angle fallbackAngle,
        Rectangle fieldBounds,
        Rectangle ownPenaltyArea,
        Rectangle oppPenaltyArea,
        float ballRadius)
    {
        var facingAngle = CalculateFacingAngle(currentBallVelocity, fallbackAngle);
        var backProjectionDistance = CalculateBackProjectionDistance(trajectory);
        var projection = ProjectReceivePoint(
            trajectory,
            currentBallPosition,
            currentBallVelocity,
            backProjectionDistance,
            preferredReceivePoint);

        var destination = GetCenterDestination(
            projection.Point,
            facingAngle,
            centerToDribbler,
            ballRadius);

        destination = ClampToLegalDestination(
            destination,
            fieldBounds,
            ownPenaltyArea,
            oppPenaltyArea,
            PenaltyAreaMarginMm,
            currentBallPosition,
            currentBallVelocity);

        return new ReceiveTarget(projection.Point, destination, facingAngle, projection.UsedBackProjection);
    }

    public static bool ShouldLockFacing(Vector2 ballPosition, Vector2 receivePoint)
    {
        return Vector2.DistanceSquared(ballPosition, receivePoint) <=
               OrientationLockDistanceMm * OrientationLockDistanceMm;
    }

    private static Vector2 ClampInside(Vector2 point, Rectangle bounds)
    {
        return new Vector2(
            Math.Clamp(point.X, bounds.Min.X, bounds.Max.X),
            Math.Clamp(point.Y, bounds.Min.Y, bounds.Max.Y));
    }

    private static Vector2 ClampOutsidePenaltyArea(
        Vector2 destination,
        Rectangle penaltyArea,
        float penaltyMargin,
        Vector2? ballPosition,
        Vector2? ballVelocity)
    {
        if (!penaltyArea.Inside(destination, penaltyMargin))
        {
            return destination;
        }

        if (ballPosition.HasValue &&
            ballVelocity.HasValue &&
            !Utils.ApproximatelyZero(ballVelocity.Value.LengthSquared()) &&
            TryProjectOutsidePenaltyAreaOnBallPath(
                destination,
                penaltyArea,
                penaltyMargin,
                ballPosition.Value,
                ballVelocity.Value,
                out var projected))
        {
            return projected;
        }

        if (ballPosition.HasValue && penaltyArea.Inside(ballPosition.Value, penaltyMargin))
        {
            return penaltyArea.NearestOutside(ballPosition.Value, penaltyMargin + LegalSeparationEpsilonMm);
        }

        return penaltyArea.NearestOutside(destination, penaltyMargin + LegalSeparationEpsilonMm);
    }

    private static bool TryProjectOutsidePenaltyAreaOnBallPath(
        Vector2 destination,
        Rectangle penaltyArea,
        float penaltyMargin,
        Vector2 ballPosition,
        Vector2 ballVelocity,
        out Vector2 projected)
    {
        projected = default;

        var pathLine = GetBallPathLine(destination, ballPosition, ballVelocity);
        if (!pathLine.HasValue)
        {
            return false;
        }

        var expandedPenaltyArea = Expand(penaltyArea, penaltyMargin + LegalSeparationEpsilonMm);
        var (intersection0, intersection1) = Geometry.Intersection(expandedPenaltyArea, pathLine.Value);

        if (!intersection0.HasValue && !intersection1.HasValue)
        {
            return false;
        }

        projected = NearestTo(destination, intersection0, intersection1);
        return true;
    }

    private static Line? GetBallPathLine(Vector2 destination, Vector2 ballPosition, Vector2 ballVelocity)
    {
        var toBall = ballPosition - destination;
        if (toBall.LengthSquared() > 100f * 100f)
        {
            return Line.FromTwoPoints(destination, ballPosition);
        }

        if (!Utils.ApproximatelyZero(ballVelocity.LengthSquared()))
        {
            return Line.FromPointAndAngle(destination, ballVelocity.ToAngle());
        }

        if (!Utils.ApproximatelyZero(toBall.LengthSquared()))
        {
            return Line.FromTwoPoints(destination, ballPosition);
        }

        return null;
    }

    private static Vector2 NearestTo(Vector2 reference, Vector2? point0, Vector2? point1)
    {
        if (!point0.HasValue) return point1!.Value;
        if (!point1.HasValue) return point0.Value;

        return Vector2.DistanceSquared(reference, point0.Value) <= Vector2.DistanceSquared(reference, point1.Value)
            ? point0.Value
            : point1.Value;
    }

    private static Rectangle Shrink(Rectangle bounds, float margin)
    {
        if (margin <= 0f)
        {
            return bounds;
        }

        var halfWidth = MathF.Max(0f, bounds.Width * 0.5f - margin);
        var halfHeight = MathF.Max(0f, bounds.Height * 0.5f - margin);
        return Rectangle.FromCenterAndSize(bounds.Center, halfWidth * 2f, halfHeight * 2f);
    }

    private static Rectangle Expand(Rectangle bounds, float margin)
    {
        if (margin <= 0f)
        {
            return bounds;
        }

        return Rectangle.FromCenterAndSize(
            bounds.Center,
            bounds.Width + margin * 2f,
            bounds.Height + margin * 2f);
    }
}

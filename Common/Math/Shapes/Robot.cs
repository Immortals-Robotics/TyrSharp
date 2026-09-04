using System.Numerics;
using Tyr.Common.Config;

namespace Tyr.Common.Math.Shapes;

[Configurable]
public readonly partial record struct Robot
{
    [ConfigEntry("Default distance from robot center to dribbler [mm]")]
    public static partial float DefaultCenterToDribbler { get; set; } = 75f;

    private const float DefaultKickerDepth = 150f;
    private const float BoundaryTolerance = 1e-3f;

    public Vector2 Center { get; init; }
    public float Radius { get; init; }
    public Angle Angle { get; init; }
    public float CenterToDribbler { get; init; }

    public readonly float FrontDistance => EffectiveCenterToDribbler;
    public readonly float EffectiveCenterToDribbler =>
        System.Math.Clamp(
            CenterToDribbler > 0f
                ? CenterToDribbler
                : Radius * (DefaultCenterToDribbler / 90f),
            0f,
            Radius);

    public readonly Vector2 KickerCenter => GetKickerCenterPos(Center, Angle, EffectiveCenterToDribbler);

    public readonly float KickerWidth =>
        2f * MathF.Sqrt(MathF.Max(0f, Radius * Radius - EffectiveCenterToDribbler * EffectiveCenterToDribbler));

    public static Vector2 GetKickerCenterPos(Vector2 center, Angle angle, float centerToDribbler)
    {
        return center + angle.ToUnitVec() * centerToDribbler;
    }

    public readonly bool CanKick(Vector2 point, float kickerDepth = DefaultKickerDepth)
    {
        return IsPointInKickerZone(point, kickerDepth);
    }

    public readonly bool IsPointInKickerZone(Vector2 point, float zoneLength)
    {
        return IsPointInKickerZone(point, zoneLength, KickerWidth);
    }

    public readonly bool IsPointInKickerZone(Vector2 point, float zoneLength, float zoneWidth)
    {
        var forward = Angle.ToUnitVec();
        var lateral = new Vector2(-forward.Y, forward.X);
        var relative = point - Center;

        var leadDistance = Vector2.Dot(relative, forward);
        if (leadDistance <= 0f)
        {
            return false;
        }

        if (leadDistance < EffectiveCenterToDribbler || leadDistance > EffectiveCenterToDribbler + zoneLength)
        {
            return false;
        }

        var lateralDistance = MathF.Abs(Vector2.Dot(relative, lateral));
        return lateralDistance <= zoneWidth * 0.5f;
    }

    public readonly LineSegment GetFrontLine()
    {
        if (Radius <= 0f)
        {
            return new LineSegment { Start = Center, End = Center };
        }

        var angleToCorner = Angle.FromRad(MathF.Acos(System.Math.Clamp(EffectiveCenterToDribbler / Radius, -1f, 1f)));
        var p1 = Center + (Angle + angleToCorner).ToUnitVec() * Radius;
        var p2 = Center + (Angle - angleToCorner).ToUnitVec() * Radius;
        return new LineSegment { Start = p1, End = p2 };
    }

    public readonly float Distance(Vector2 point)
    {
        var closestBoundary = ClosestBoundaryPoint(point);
        var distance = Vector2.Distance(point, closestBoundary);
        return Contains(point) ? -distance : distance;
    }

    public readonly bool Inside(Vector2 point, float margin = 0)
    {
        return margin <= 0f
            ? Contains(point)
            : WithMargin(margin).Contains(point);
    }

    public readonly Vector2 NearestOutside(Vector2 point, float margin = 0)
    {
        var robot = margin <= 0f ? this : WithMargin(margin);
        return robot.Contains(point)
            ? robot.ClosestBoundaryPoint(point)
            : point;
    }

    public readonly Robot WithMargin(float margin)
    {
        if (Radius <= 0f)
        {
            return this;
        }

        var factor = (Radius + margin) / Radius;
        if (factor <= 0f)
        {
            return this with { Radius = 0f, CenterToDribbler = 0f };
        }

        return this with
        {
            Radius = Radius * factor,
            CenterToDribbler = EffectiveCenterToDribbler * factor
        };
    }

    private readonly bool Contains(Vector2 point)
    {
        if (Vector2.Distance(Center, point) > Radius + BoundaryTolerance)
        {
            return false;
        }

        return !IsPointInKickerZone(point, Radius) || GetFrontLine().Distance(point) <= BoundaryTolerance;
    }

    private readonly Vector2 ClosestBoundaryPoint(Vector2 point)
    {
        var frontLine = GetFrontLine();
        var closestFrontPoint = frontLine.ClosestPoint(point);

        var radialDirection = point - Center;
        var closestCirclePoint = Utils.ApproximatelyZero(radialDirection.LengthSquared())
            ? KickerCenter
            : Center + Vector2.Normalize(radialDirection) * Radius;

        if (ProjectsOnto(frontLine, point) &&
            Vector2.DistanceSquared(point, closestFrontPoint) <= Vector2.DistanceSquared(point, closestCirclePoint))
        {
            return closestFrontPoint;
        }

        return closestCirclePoint;
    }

    private static bool ProjectsOnto(LineSegment segment, Vector2 point)
    {
        var direction = segment.End - segment.Start;
        var lengthSquared = direction.LengthSquared();
        if (Utils.ApproximatelyZero(lengthSquared))
        {
            return false;
        }

        var t = Vector2.Dot(point - segment.Start, direction) / lengthSquared;
        return t >= 0f && t <= 1f;
    }
}

using System.Numerics;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;

namespace Tyr.Vision.Data;

public readonly record struct RobotCollisionShape
{
    public Vector2 Position { get; init; }
    public Angle Orientation { get; init; }
    public Vector2 Velocity { get; init; }
    public float Radius { get; init; }
    public float CenterToDribbler { get; init; }
    public float MaxCircleLoss { get; init; }
    public float MaxFrontLoss { get; init; }

    public RobotCollisionResult GetCollision(Vector2 ballPosition, Vector2 ballVelocity)
    {
        var robotShape = new Robot
        {
            Center = Position,
            Radius = Radius,
            Angle = Orientation,
            CenterToDribbler = CenterToDribbler
        };

        if (!robotShape.Inside(ballPosition))
        {
            return new RobotCollisionResult { Location = RobotCollisionLocation.None };
        }

        var ballVelocityUsed = ballVelocity;
        if (ballVelocityUsed.Length() < 10f)
        {
            var botCircle = new Circle
            {
                Center = Position,
                Radius = Radius + BallParameters.Radius
            };

            var outside = botCircle.NearestOutside(ballPosition);
            ballVelocityUsed = ballPosition - outside;
        }

        ballVelocityUsed -= Velocity;

        var ballVelocityLine = CreateBallVelocityLine(ballPosition, ballVelocityUsed);

        var frontRobot = robotShape with
        {
            Radius = Radius + BallParameters.Radius,
            CenterToDribbler = CenterToDribbler + BallParameters.Radius
        };

        if (TryIntersect(frontRobot.GetFrontLine(), ballVelocityLine, out _))
        {
            var outwardNormal = -Orientation.ToUnitVec();
            var collisionAngle = SignedAngle(outwardNormal, ballVelocityUsed);
            var collisionAngleAbs = MathF.Abs(collisionAngle);
            if (collisionAngleAbs > MathF.PI * 0.5f)
            {
                return new RobotCollisionResult { Location = RobotCollisionLocation.Circle };
            }

            var outSpeed = DampedSpeed(ballVelocityUsed.Length(), collisionAngleAbs, MaxFrontLoss);
            var outDirection = outwardNormal
                .Rotated(Angle.FromRad(-collisionAngle))
                * -1f;

            return new RobotCollisionResult
            {
                Location = RobotCollisionLocation.Front,
                BallReflectedVelocity = outDirection.WithLength(outSpeed) + Velocity
            };
        }

        var botHull = new Circle
        {
            Center = Position,
            Radius = Radius + BallParameters.Radius
        };

        var circleIntersection = FirstCircleIntersection(botHull, ballVelocityLine);
        if (!circleIntersection.HasValue)
        {
            return new RobotCollisionResult { Location = RobotCollisionLocation.None };
        }

        var collisionNormal = Position - circleIntersection.Value;
        var circleCollisionAngle = SignedAngle(collisionNormal, ballVelocityUsed);
        var circleCollisionAngleAbs = MathF.Abs(circleCollisionAngle);
        if (circleCollisionAngleAbs > MathF.PI * 0.5f)
        {
            return new RobotCollisionResult { Location = RobotCollisionLocation.Circle };
        }

        var circleOutSpeed = DampedSpeed(ballVelocityUsed.Length(), circleCollisionAngleAbs, MaxCircleLoss);
        var circleOutDirection = collisionNormal
            .Rotated(Angle.FromRad(-circleCollisionAngle))
            * -1f;

        return new RobotCollisionResult
        {
            Location = RobotCollisionLocation.Circle,
            BallReflectedVelocity = circleOutDirection.WithLength(circleOutSpeed) + Velocity
        };
    }

    private LineSegment CreateBallVelocityLine(Vector2 ballPosition, Vector2 ballVelocity)
    {
        if (Utils.ApproximatelyZero(ballVelocity.LengthSquared()))
        {
            return new LineSegment
            {
                Start = ballPosition,
                End = ballPosition
            };
        }

        return new LineSegment
        {
            Start = ballPosition,
            End = ballPosition + (-ballVelocity).WithLength(Radius * 5f)
        };
    }

    private static float SignedAngle(Vector2 from, Vector2 to)
    {
        if (Utils.ApproximatelyZero(from.LengthSquared()) || Utils.ApproximatelyZero(to.LengthSquared()))
        {
            return MathF.PI;
        }

        return MathF.Atan2(from.Cross(to), Vector2.Dot(from, to));
    }

    private static float DampedSpeed(float speed, float collisionAngleAbs, float maxLoss)
    {
        return MathF.Max(0f, speed - speed * MathF.Cos(collisionAngleAbs) * maxLoss);
    }

    private static Vector2? FirstCircleIntersection(Circle circle, LineSegment segment)
    {
        var line = Line.FromSegment(segment);
        var (first, second) = Geometry.Intersection(line, circle);

        Vector2? best = null;
        var bestDistance = float.PositiveInfinity;

        foreach (var point in new[] { first, second })
        {
            if (!point.HasValue || !PointOnSegment(segment, point.Value))
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(segment.Start, point.Value);
            if (distance >= bestDistance)
            {
                continue;
            }

            best = point.Value;
            bestDistance = distance;
        }

        return best;
    }

    private static bool TryIntersect(LineSegment first, LineSegment second, out Vector2 intersection)
    {
        var p = first.Start;
        var r = first.End - first.Start;
        var q = second.Start;
        var s = second.End - second.Start;

        var rCrossS = r.Cross(s);
        if (Utils.ApproximatelyZero(rCrossS))
        {
            intersection = default;
            return false;
        }

        var qMinusP = q - p;
        var t = qMinusP.Cross(s) / rCrossS;
        var u = qMinusP.Cross(r) / rCrossS;
        if (t < 0f || t > 1f || u < 0f || u > 1f)
        {
            intersection = default;
            return false;
        }

        intersection = p + r * t;
        return true;
    }

    private static bool PointOnSegment(LineSegment segment, Vector2 point)
    {
        var minX = MathF.Min(segment.Start.X, segment.End.X) - 1e-3f;
        var maxX = MathF.Max(segment.Start.X, segment.End.X) + 1e-3f;
        var minY = MathF.Min(segment.Start.Y, segment.End.Y) - 1e-3f;
        var maxY = MathF.Max(segment.Start.Y, segment.End.Y) + 1e-3f;

        return point.X >= minX &&
               point.X <= maxX &&
               point.Y >= minY &&
               point.Y <= maxY;
    }
}

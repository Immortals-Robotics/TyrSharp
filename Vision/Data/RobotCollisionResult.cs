using System.Numerics;

namespace Tyr.Vision.Data;

public readonly record struct RobotCollisionResult
{
    public RobotCollisionLocation Location { get; init; }
    public Vector2? BallReflectedVelocity { get; init; }
}

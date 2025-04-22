using System.Numerics;

namespace Tyr.Vision.Tracker;

public readonly record struct MergedBall
{
    public Vector2 Position { get; init; }
    public Vector2 RawPosition { get; init; }
    public Vector2 Velocity { get; init; }
    public Timestamp Timestamp { get; init; }
    public RawBall? LatestRawBall { get; init; }
}
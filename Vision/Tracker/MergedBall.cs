using System.Numerics;
using RawBall = Tyr.Vision.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;

namespace Tyr.Vision.Tracker;

public readonly record struct MergedBall
{
    public Vector2 Position { get; init; }
    public Vector2 RawPosition { get; init; }
    public Vector2 Velocity { get; init; }
    public DateTime Time { get; init; }
    public RawBall? LatestRawBall { get; init; }
}
using System.Numerics;
using Tyr.Common.Time;

namespace Tyr.Common.Vision.Data;

public readonly record struct BallTouchdown(Vector2 Position, DeltaTime TimeUntilTouchdown)
{
    public Timestamp GetTimestamp(Timestamp originTimestamp) => originTimestamp + TimeUntilTouchdown;
}

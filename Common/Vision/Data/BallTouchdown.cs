using System.Numerics;
using Tyr.Common.Time;
using MemoryPack;

namespace Tyr.Common.Vision.Data;

[MemoryPackable]
public partial record struct BallTouchdown(Vector2 Position, DeltaTime TimeUntilTouchdown)
{
    public Timestamp GetTimestamp(Timestamp originTimestamp) => originTimestamp + TimeUntilTouchdown;
}

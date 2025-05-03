using System.Numerics;
using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// Game was stuck
[ProtoContract]
public class NoProgressInGame
{
    /// The location of the ball
    [ProtoMember(1)] public Vector2? Location { get; set; }

    /// The time [s] that was waited
    [ProtoMember(2)] public float? TimeSeconds { get; set; }

    public DeltaTime? Time => TimeSeconds.HasValue
        ? DeltaTime.FromSeconds(TimeSeconds.Value)
        : null;
}
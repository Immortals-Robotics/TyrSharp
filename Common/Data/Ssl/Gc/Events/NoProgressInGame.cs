using System.Numerics;
using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class NoProgressInGame
{
    [ProtoMember(1)] public Vector2? Location { get; set; }

    [ProtoMember(2)] public float? TimeSeconds { get; set; }

    public DeltaTime? Time => TimeSeconds.HasValue
        ? DeltaTime.FromSeconds(TimeSeconds.Value)
        : null;
}
using System.Numerics;
using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class KickTimeout
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    [ProtoMember(2)] public Vector2? Location { get; set; }

    [ProtoMember(3)] public float? TimeSeconds { get; set; }
    public DeltaTime? Time => TimeSeconds.HasValue ? DeltaTime.FromSeconds(TimeSeconds.Value) : null;
}
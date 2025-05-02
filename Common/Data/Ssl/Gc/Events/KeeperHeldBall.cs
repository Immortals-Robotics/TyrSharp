using System.Numerics;
using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class KeeperHeldBall
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    [ProtoMember(2)] public Vector2? Location { get; set; }
    [ProtoMember(3)] public float? DurationSeconds { get; set; }

    public DeltaTime? Duration => DurationSeconds.HasValue
        ? DeltaTime.FromSeconds(DurationSeconds.Value)
        : null;
}
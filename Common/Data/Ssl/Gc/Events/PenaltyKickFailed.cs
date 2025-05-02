using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class PenaltyKickFailed
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    [ProtoMember(2)] public Vector2? Location { get; set; }
    [ProtoMember(3)] public string? Reason { get; set; }
}
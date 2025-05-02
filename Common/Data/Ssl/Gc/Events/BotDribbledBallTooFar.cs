using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class BotDribbledBallTooFar
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    [ProtoMember(2)] public uint? ByBot { get; set; }
    [ProtoMember(3)] public Vector2? Start { get; set; }
    [ProtoMember(4)] public Vector2? End { get; set; }
}
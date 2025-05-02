using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class BotKickedBallTooFast
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    [ProtoMember(2)] public uint? ByBot { get; set; }
    [ProtoMember(3)] public Vector2? Location { get; set; }
    [ProtoMember(4)] public float? InitialBallSpeed { get; set; }
    [ProtoMember(5)] public bool? Chipped { get; set; }
}
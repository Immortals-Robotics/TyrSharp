using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class BotPushedBot
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    [ProtoMember(2)] public uint? Violator { get; set; }
    [ProtoMember(3)] public uint? Victim { get; set; }
    [ProtoMember(4)] public Vector2? Location { get; set; }
    [ProtoMember(5)] public float? PushedDistance { get; set; }
}
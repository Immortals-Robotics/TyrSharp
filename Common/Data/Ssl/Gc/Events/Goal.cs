using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class Goal
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    [ProtoMember(6)] public TeamColor? KickingTeam { get; set; }
    [ProtoMember(2)] public uint? KickingBot { get; set; }
    [ProtoMember(3)] public Vector2? Location { get; set; }
    [ProtoMember(4)] public Vector2? KickLocation { get; set; }
    [ProtoMember(5)] public float? MaxBallHeight { get; set; }
    [ProtoMember(7)] public uint? NumRobotsByTeam { get; set; }
    [ProtoMember(8)] public ulong? LastTouchByTeam { get; set; }
    [ProtoMember(9)] public string? Message { get; set; }
}
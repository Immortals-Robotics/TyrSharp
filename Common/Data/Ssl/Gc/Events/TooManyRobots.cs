using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class TooManyRobots
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    [ProtoMember(2)] public int? NumRobotsAllowed { get; set; }
    [ProtoMember(3)] public int? NumRobotsOnField { get; set; }
    [ProtoMember(4)] public Vector2? BallLocation { get; set; }
}
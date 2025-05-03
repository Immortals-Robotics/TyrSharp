using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A team has too many robots on the field
[ProtoContract]
public class TooManyRobots
{
    /// The team that has too many robots
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// Number of robots allowed at the moment
    [ProtoMember(2)] public int? NumRobotsAllowed { get; set; }
    
    /// Number of robots currently on the field
    [ProtoMember(3)] public int? NumRobotsOnField { get; set; }
    
    /// The location of the ball at the moment when this foul occurred [m]
    [ProtoMember(4)] public Vector2? BallLocation { get; set; }
}
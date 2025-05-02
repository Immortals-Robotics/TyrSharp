using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// The penalty kick failed (by time or by keeper)
[ProtoContract]
public class PenaltyKickFailed
{
    /// The team that last touched the ball
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The location of the ball at the moment of this event [m]
    [ProtoMember(2)] public Vector2? Location { get; set; }
    
    /// An explanation of the failure
    [ProtoMember(3)] public string? Reason { get; set; }
}
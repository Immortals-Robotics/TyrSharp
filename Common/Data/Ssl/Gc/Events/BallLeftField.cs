using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// The ball left the field normally
[ProtoContract]
public class BallLeftField
{
    /// The team that last touched the ball
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The bot that last touched the ball
    [ProtoMember(2)] public uint? ByBot { get; set; }
    
    /// The location where the ball left the field [m]
    [ProtoMember(3)] public Vector2? Location { get; set; }
}
using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// An attacker touched the ball multiple times when it was not allowed to
[ProtoContract]
public class AttackerDoubleTouchedBall
{
    /// The team that found guilty
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The bot that touched the ball twice
    [ProtoMember(2)] public uint? ByBot { get; set; }
    
    /// The location of the ball when it was first touched [m]
    [ProtoMember(3)] public Vector2? Location { get; set; }
}
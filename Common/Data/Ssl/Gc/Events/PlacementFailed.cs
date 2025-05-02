using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// Ball placement failed
[ProtoContract]
public class PlacementFailed
{
    /// The team that failed
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The remaining distance [m] from ball to placement position
    [ProtoMember(2)] public float? RemainingDistance { get; set; }
    
    /// The distance [m] of the nearest own robot to the ball
    [ProtoMember(3)] public float? NearestOwnBotDistance { get; set; }
}
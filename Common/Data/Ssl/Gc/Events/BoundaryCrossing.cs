using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A robot chipped the ball over the field boundary out of the playing surface
[ProtoContract]
public class BoundaryCrossing
{
    /// The team that has too many robots
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The location of the ball [m]
    [ProtoMember(2)] public Vector2? Location { get; set; }
}
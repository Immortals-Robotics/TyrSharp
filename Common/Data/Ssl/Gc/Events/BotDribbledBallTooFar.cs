using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A bot dribbled to ball too far
[ProtoContract]
public class BotDribbledBallTooFar
{
    /// The team that found guilty
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The bot that dribbled too far
    [ProtoMember(2)] public uint? ByBot { get; set; }
    
    /// The location where the dribbling started [m]
    [ProtoMember(3)] public Vector2? Start { get; set; }
    
    /// The location where the maximum dribbling distance was reached [m]
    [ProtoMember(4)] public Vector2? End { get; set; }
}
using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A bot pushed another bot over a significant distance
[ProtoContract]
public class BotPushedBot
{
    /// The team that pushed the other team
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The bot that pushed the other bot
    [ProtoMember(2)] public uint? Violator { get; set; }
    
    /// The bot of the opposite team that was pushed
    [ProtoMember(3)] public uint? Victim { get; set; }
    
    /// The location of the push (center between both bots) [m]
    [ProtoMember(4)] public Vector2? Location { get; set; }
    
    /// The pushed distance [m]
    [ProtoMember(5)] public float? PushedDistance { get; set; }
}
using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A bot kicked the ball too fast
[ProtoContract]
public class BotKickedBallTooFast
{
    /// The team that found guilty
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The bot that kicked too fast
    [ProtoMember(2)] public uint? ByBot { get; set; }
    
    /// The location of the ball at the time of the highest speed [m]
    [ProtoMember(3)] public Vector2? Location { get; set; }
    
    /// The absolute initial ball speed (kick speed) [m/s]
    [ProtoMember(4)] public float? InitialBallSpeed { get; set; }
    
    /// Was the ball chipped?
    [ProtoMember(5)] public bool? Chipped { get; set; }
}
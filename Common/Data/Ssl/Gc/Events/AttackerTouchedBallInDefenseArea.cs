using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// An attacker touched the ball inside the opponent defense area
[ProtoContract]
public class AttackerTouchedBallInDefenseArea
{
    /// The team that found guilty
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The bot that is inside the penalty area
    [ProtoMember(2)] public uint? ByBot { get; set; }
    
    /// The location of the bot [m]
    [ProtoMember(3)] public Vector2? Location { get; set; }
    
    /// The distance [m] that the bot is inside the penalty area
    [ProtoMember(4)] public float? Distance { get; set; }
}
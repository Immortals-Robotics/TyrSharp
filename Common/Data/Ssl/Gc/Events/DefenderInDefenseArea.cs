using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A defender other than the keeper was fully located inside its own defense and touched the ball
[ProtoContract]
public class DefenderInDefenseArea
{
    /// The team that found guilty
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The bot that is inside the penalty area
    [ProtoMember(2)] public uint? ByBot { get; set; }
    
    /// The location of the bot [m]
    [ProtoMember(3)] public Vector2? Location { get; set; }
    
    /// The distance [m] from bot case to the nearest point outside the defense area
    [ProtoMember(4)] public float? Distance { get; set; }
}
using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// Two robots crashed into each other and one team was found guilty to due significant speed difference
[ProtoContract]
public class BotCrashUnique
{
    /// The team that caused the crash
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The bot that caused the crash
    [ProtoMember(2)] public uint? Violator { get; set; }
    
    /// The bot of the opposite team that was involved in the crash
    [ProtoMember(3)] public uint? Victim { get; set; }
    
    /// The location of the crash (center between both bots) [m]
    [ProtoMember(4)] public Vector2? Location { get; set; }
    
    /// The calculated crash speed vector [m/s] of the two bots
    [ProtoMember(5)] public float? CrashSpeed { get; set; }
    
    /// The difference [m/s] of the velocity of the two bots
    [ProtoMember(6)] public float? SpeedDiff { get; set; }
    
    /// The angle [rad] in the range [0, π] of the bot velocity vectors
    /// An angle of 0 rad (  0°) means, the bots barely touched each other
    /// An angle of π rad (180°) means, the bots crashed frontal into each other
    [ProtoMember(7)] public float? CrashAngle { get; set; }
}
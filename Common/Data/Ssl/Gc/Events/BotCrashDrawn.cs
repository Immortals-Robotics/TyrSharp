using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// Two robots crashed into each other with similar speeds
[ProtoContract]
public class BotCrashDrawn
{
    /// The bot of the yellow team
    [ProtoMember(1)] public uint? BotYellow { get; set; }
    
    /// The bot of the blue team
    [ProtoMember(2)] public uint? BotBlue { get; set; }
    
    /// The location of the crash (center between both bots) [m]
    [ProtoMember(3)] public Vector2? Location { get; set; }
    
    /// The calculated crash speed [m/s] of the two bots
    [ProtoMember(4)] public float? CrashSpeed { get; set; }
    
    /// The difference [m/s] of the velocity of the two bots
    [ProtoMember(5)] public float? SpeedDiff { get; set; }
    
    /// The angle [rad] in the range [0, π] of the bot velocity vectors
    /// An angle of 0 rad (  0°) means, the bots barely touched each other
    /// An angle of π rad (180°) means, the bots crashed frontal into each other
    [ProtoMember(6)] public float? CrashAngle { get; set; }
}
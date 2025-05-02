using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class BotCrashDrawn
{
    [ProtoMember(1)] public uint? BotYellow { get; set; }
    [ProtoMember(2)] public uint? BotBlue { get; set; }
    [ProtoMember(3)] public Vector2? Location { get; set; }
    [ProtoMember(4)] public float? CrashSpeed { get; set; }
    [ProtoMember(5)] public float? SpeedDiff { get; set; }
    [ProtoMember(6)] public float? CrashAngle { get; set; }
}
using ProtoBuf;
using Tyr.Common.Math;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

[ProtoContract]
public struct KickedBall
{
    [ProtoMember(1, IsRequired = true)] public Vector2 Pos { get; set; }
    [ProtoMember(2, IsRequired = true)] public Vector3 Vel { get; set; }

    [ProtoMember(3, IsRequired = true)] public double StartTimestampSeconds { get; set; }
    public DateTime StartTimestamp => UnixTime.FromSeconds(StartTimestampSeconds);

    [ProtoMember(4)] public double? StopTimestampSeconds { get; set; }

    public DateTime? StopTimestamp => StopTimestampSeconds.HasValue
        ? UnixTime.FromSeconds(StopTimestampSeconds.Value)
        : null;

    [ProtoMember(5)] public Vector2? StopPos { get; set; }

    [ProtoMember(6)] public RobotId? RobotId { get; set; }
}
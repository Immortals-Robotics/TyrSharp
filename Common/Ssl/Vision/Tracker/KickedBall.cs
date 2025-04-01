using ProtoBuf;

namespace Tyr.Common.Ssl.Vision.Tracker;

[ProtoContract]
public class KickedBall
{
    [ProtoMember(1, IsRequired = true)] public Vector2 Pos { get; set; } = new();
    [ProtoMember(2, IsRequired = true)] public Vector3 Vel { get; set; } = new();

    [ProtoMember(3, IsRequired = true)] public double StartTimestamp { get; set; }
    [ProtoMember(4)] public double? StopTimestamp { get; set; }

    [ProtoMember(5)] public Vector2? StopPos { get; set; }

    [ProtoMember(6)] public RobotId? RobotId { get; set; }
}
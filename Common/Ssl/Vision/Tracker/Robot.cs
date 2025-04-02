using ProtoBuf;

namespace Tyr.Common.Ssl.Vision.Tracker;

[ProtoContract]
public struct Robot
{
    [ProtoMember(1, IsRequired = true)] public RobotId RobotId { get; set; }

    [ProtoMember(2, IsRequired = true)] public Vector2 Pos { get; set; }

    [ProtoMember(3, IsRequired = true)] public float Orientation { get; set; }

    [ProtoMember(4)] public Vector2? Vel { get; set; }

    [ProtoMember(5)] public float? VelAngular { get; set; }

    [ProtoMember(6)] public float? Visibility { get; set; }
}
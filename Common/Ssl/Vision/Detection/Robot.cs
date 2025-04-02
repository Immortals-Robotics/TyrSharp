using ProtoBuf;

namespace Tyr.Common.Ssl.Vision.Detection;

[ProtoContract]
public struct Robot
{
    [ProtoMember(1, IsRequired = true)] public float Confidence { get; set; }

    [ProtoMember(2)] public uint? RobotId { get; set; }

    [ProtoMember(3, IsRequired = true)] public float X { get; set; }
    [ProtoMember(4, IsRequired = true)] public float Y { get; set; }

    [ProtoMember(5)] public float? Orientation { get; set; }

    [ProtoMember(6, IsRequired = true)] public float PixelX { get; set; }
    [ProtoMember(7, IsRequired = true)] public float PixelY { get; set; }

    [ProtoMember(8)] public float? Height { get; set; }
}
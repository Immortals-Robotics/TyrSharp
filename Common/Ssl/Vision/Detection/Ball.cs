using ProtoBuf;

namespace Tyr.Common.Ssl.Vision.Detection;

[ProtoContract]
public class Ball
{
    [ProtoMember(1, IsRequired = true)] public float Confidence { get; set; }

    [ProtoMember(2)] public uint? Area { get; set; }

    [ProtoMember(3, IsRequired = true)] public float X { get; set; }
    [ProtoMember(4, IsRequired = true)] public float Y { get; set; }
    [ProtoMember(5)] public float? Z { get; set; }

    [ProtoMember(6, IsRequired = true)] public float PixelX { get; set; }
    [ProtoMember(7, IsRequired = true)] public float PixelY { get; set; }
}
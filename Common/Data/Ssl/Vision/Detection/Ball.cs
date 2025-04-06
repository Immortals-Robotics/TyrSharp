using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Data.Ssl.Vision.Detection;

[ProtoContract]
public struct Ball
{
    [ProtoMember(1, IsRequired = true)] public float Confidence { get; set; }

    [ProtoMember(2)] public uint? Area { get; set; }

    [ProtoMember(3, IsRequired = true)] public float X { get; set; }
    [ProtoMember(4, IsRequired = true)] public float Y { get; set; }
    [ProtoMember(5)] public float? Z { get; set; }
    public Vector3 Position => new(X, Y, Z.GetValueOrDefault());

    [ProtoMember(6, IsRequired = true)] public float PixelX { get; set; }
    [ProtoMember(7, IsRequired = true)] public float PixelY { get; set; }
    public Vector2 PixelPosition => new(PixelX, PixelY);
}
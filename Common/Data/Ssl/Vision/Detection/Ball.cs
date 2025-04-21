using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Detection;

[ProtoContract]
public struct Ball : IObject
{
    [ProtoMember(1, IsRequired = true)] public float Confidence { get; set; }

    [ProtoMember(2)] public uint? Area { get; set; }

    [ProtoMember(3, IsRequired = true)] public float X { get; set; }
    [ProtoMember(4, IsRequired = true)] public float Y { get; set; }
    [ProtoMember(5)] public float? Z { get; set; }

    // z is always 0 in the current ssl-vision
    public System.Numerics.Vector2 Position => new(X, Y);

    [ProtoMember(6, IsRequired = true)] public float PixelX { get; set; }
    [ProtoMember(7, IsRequired = true)] public float PixelY { get; set; }
    public System.Numerics.Vector2 PixelPosition => new(PixelX, PixelY);
}
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Detection;

[ProtoContract]
public struct Robot : IObject
{
    [ProtoMember(1, IsRequired = true)] public float Confidence { get; set; }

    [ProtoMember(2)] public uint? RobotId { get; set; }

    [ProtoMember(3, IsRequired = true)] public float X { get; set; }
    [ProtoMember(4, IsRequired = true)] public float Y { get; set; }
    public System.Numerics.Vector2 Position => new(X, Y);

    [ProtoMember(5)] public float? OrientationRad { get; set; }
    public Math.Angle? Orientation => OrientationRad.HasValue ? Math.Angle.FromRad(OrientationRad.Value) : null;

    [ProtoMember(6, IsRequired = true)] public float PixelX { get; set; }
    [ProtoMember(7, IsRequired = true)] public float PixelY { get; set; }
    public System.Numerics.Vector2 PixelPosition => new(PixelX, PixelY);

    [ProtoMember(8)] public float? Height { get; set; }
}
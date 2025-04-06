using ProtoBuf;
using Tyr.Common.Math;
using Tyr.Common.Shape;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

[ProtoContract]
public struct FieldCircularArc
{
    [ProtoMember(1, IsRequired = true)] public string Name { get; set; }

    [ProtoMember(2, IsRequired = true)] public Vector2 Center { get; set; }
    [ProtoMember(3, IsRequired = true)] public float Radius { get; set; }
    public Circle Circle => new Circle(Center, Radius);

    [ProtoMember(4, IsRequired = true)] public float A1 { get; set; }
    [ProtoMember(5, IsRequired = true)] public float A2 { get; set; }
    [ProtoMember(6, IsRequired = true)] public float Thickness { get; set; }
    [ProtoMember(7)] public FieldShapeType? Type { get; set; }
}
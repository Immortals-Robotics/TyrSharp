using ProtoBuf;
using Tyr.Common.Shape;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

[ProtoContract]
public struct FieldLineSegment
{
    [ProtoMember(1, IsRequired = true)] public string Name { get; set; }

    [ProtoMember(2, IsRequired = true)] public Vector2 P1Raw { get; set; }
    public Vector2 P1 => P1Raw;

    [ProtoMember(3, IsRequired = true)] public Vector2 P2Raw { get; set; }
    public Vector2 P2 => P2Raw;

    public LineSegment LineSegment => new(P1, P2);

    [ProtoMember(4, IsRequired = true)] public float Thickness { get; set; }
    [ProtoMember(5)] public FieldShapeType? Type { get; set; }
}
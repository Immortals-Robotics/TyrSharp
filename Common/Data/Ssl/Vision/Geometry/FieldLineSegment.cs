using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

[ProtoContract]
public struct FieldLineSegment
{
    [ProtoMember(1, IsRequired = true)] public string Name { get; set; }
    [ProtoMember(2, IsRequired = true)] public Vector2 P1 { get; set; }
    [ProtoMember(3, IsRequired = true)] public Vector2 P2 { get; set; }
    [ProtoMember(4, IsRequired = true)] public float Thickness { get; set; }
    [ProtoMember(5)] public FieldShapeType? Type { get; set; }
}
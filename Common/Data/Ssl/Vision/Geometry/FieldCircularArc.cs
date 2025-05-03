using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

/// <summary>
/// Represents a field marking as a circular arc segment represented by center point, a
/// start angle, an end angle, and an arc thickness.
/// </summary>
[ProtoContract]
public struct FieldCircularArc
{
    /// <summary>
    /// Name of this field marking.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public string Name { get; set; }

    /// <summary>
    /// Center point of the circular arc.
    /// </summary>
    [ProtoMember(2, IsRequired = true)] public Vector2 Center { get; set; }

    /// <summary>
    /// Radius of the arc.
    /// </summary>
    [ProtoMember(3, IsRequired = true)] public float Radius { get; set; }

    /// <summary>
    /// Start angle in counter-clockwise order.
    /// </summary>
    [ProtoMember(4, IsRequired = true)] public float A1 { get; set; }
    
    /// <summary>
    /// End angle in counter-clockwise order.
    /// </summary>
    [ProtoMember(5, IsRequired = true)] public float A2 { get; set; }
    
    /// <summary>
    /// Thickness of the arc.
    /// </summary>
    [ProtoMember(6, IsRequired = true)] public float Thickness { get; set; }
    
    /// <summary>
    /// The type of this shape.
    /// </summary>
    [ProtoMember(7)] public FieldShapeType? Type { get; set; }
}
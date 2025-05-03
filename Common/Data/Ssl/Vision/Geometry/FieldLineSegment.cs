using System.Numerics;
using ProtoBuf;
using Tyr.Common.Math.Shapes;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

/// <summary>
/// Represents a field marking as a line segment represented by a start point p1,
/// and end point p2, and a line thickness. The start and end points are along
/// the center of the line, so the thickness of the line extends by thickness / 2
/// on either side of the line.
/// </summary>
[ProtoContract]
public struct FieldLineSegment
{
    /// <summary>
    /// Name of this field marking.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public string Name { get; set; }

    /// <summary>
    /// Start point of the line segment.
    /// </summary>
    [ProtoMember(2, IsRequired = true)] public Vector2 P1 { get; set; }

    /// <summary>
    /// End point of the line segment.
    /// </summary>
    [ProtoMember(3, IsRequired = true)] public Vector2 P2 { get; set; }

    /// <summary>
    /// Line segment representation combining P1 and P2.
    /// </summary>
    public LineSegment LineSegment => new() { Start = P1, End = P2 };

    /// <summary>
    /// Thickness of the line segment.
    /// </summary>
    [ProtoMember(4, IsRequired = true)] public float Thickness { get; set; }
    
    /// <summary>
    /// The type of this shape.
    /// </summary>
    [ProtoMember(5)] public FieldShapeType? Type { get; set; }
}
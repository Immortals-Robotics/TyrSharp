using ProtoBuf;
using Tyr.Common.Math.Shapes;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

[ProtoContract]
public struct FieldSize
{
    [ProtoMember(1, IsRequired = true)] public int FieldLength { get; set; }
    [ProtoMember(2, IsRequired = true)] public int FieldWidth { get; set; }

    [ProtoMember(5, IsRequired = true)] public int BoundaryWidth { get; set; }

    public Rectangle FieldRectangle =>
        Rectangle.FromCenterAndSize(System.Numerics.Vector2.Zero, FieldLength, FieldWidth);

    public Rectangle FieldRectangleWithBoundary => Rectangle.FromCenterAndSize(System.Numerics.Vector2.Zero,
        FieldLength + BoundaryWidth, FieldWidth + BoundaryWidth);

    [ProtoMember(3, IsRequired = true)] public int GoalWidth { get; set; }
    [ProtoMember(4, IsRequired = true)] public int GoalDepth { get; set; }

    [ProtoMember(6)] public List<FieldLineSegment> FieldLines { get; set; }
    [ProtoMember(7)] public List<FieldCircularArc> FieldArcs { get; set; }

    [ProtoMember(8)] public int? PenaltyAreaDepth { get; set; }
    [ProtoMember(9)] public int? PenaltyAreaWidth { get; set; }

    [ProtoMember(10)] public int? CenterCircleRadius { get; set; }

    public Circle CenterCircle => new()
        { Center = System.Numerics.Vector2.Zero, Radius = CenterCircleRadius.GetValueOrDefault() };

    [ProtoMember(11)] public int? LineThickness { get; set; }
    [ProtoMember(12)] public int? GoalCenterToPenaltyMark { get; set; }
    [ProtoMember(13)] public int? GoalHeight { get; set; }
    [ProtoMember(14)] public float? BallRadius { get; set; }
    [ProtoMember(15)] public float? MaxRobotRadius { get; set; }
}
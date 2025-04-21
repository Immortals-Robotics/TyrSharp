using ProtoBuf;
using Tyr.Common.Math.Shapes;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

[ProtoContract]
public class FieldSize
{
    [ProtoMember(1, IsRequired = true)] public int FieldLength { get; set; }
    [ProtoMember(2, IsRequired = true)] public int FieldWidth { get; set; }

    [ProtoMember(5, IsRequired = true)] public int BoundaryWidth { get; set; }

    public Rect FieldRect => Rect.FromCenterAndSize(System.Numerics.Vector2.Zero, FieldWidth, FieldLength);

    public Rect FieldRectWithBoundary => Rect.FromCenterAndSize(System.Numerics.Vector2.Zero,
        FieldWidth + BoundaryWidth, FieldLength + BoundaryWidth);

    [ProtoMember(3, IsRequired = true)] public int GoalWidth { get; set; }
    [ProtoMember(4, IsRequired = true)] public int GoalDepth { get; set; }

    [ProtoMember(6)] public List<FieldLineSegment> FieldLines { get; set; } = [];
    [ProtoMember(7)] public List<FieldCircularArc> FieldArcs { get; set; } = [];

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
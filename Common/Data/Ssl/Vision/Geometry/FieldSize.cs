using ProtoBuf;
using Tyr.Common.Math.Shapes;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

/// <summary>
/// Contains the dimensions and markings of the SSL field.
/// </summary>
[ProtoContract]
public struct FieldSize
{
    public FieldSize()
    {
    }

    /// <summary>
    /// Field length (distance between goal lines) in mm.
    /// </summary>
    [ProtoMember(1, IsRequired = true)]
    public int FieldLength { get; set; }

    // Compatibility alias for the old Tyr naming.
    public int FullWidth
    {
        get => FieldLength;
        set => FieldLength = value;
    }

    public int Width => FieldLength / 2;

    /// <summary>
    /// Field width (distance between touch lines) in mm.
    /// </summary>
    [ProtoMember(2, IsRequired = true)]
    public int FieldWidth { get; set; }

    // Compatibility alias for the old Tyr naming.
    public int FullHeight
    {
        get => FieldWidth;
        set => FieldWidth = value;
    }

    public int Height => FieldWidth / 2;

    /// <summary>
    /// Boundary width (distance from touch/goal line centers to boundary walls) in mm.
    /// </summary>
    [ProtoMember(5, IsRequired = true)]
    public int BoundaryWidth { get; set; }

    /// <summary>
    /// Rectangle representing the field boundaries without the boundary width.
    /// </summary>
    public Rectangle Rectangle =>
        Rectangle.FromCenterAndSize(System.Numerics.Vector2.Zero, FieldLength, FieldWidth);

    /// <summary>
    /// Rectangle representing the field boundaries including the boundary width.
    /// </summary>
    public Rectangle RectangleWithBoundary => Rectangle.FromCenterAndSize(System.Numerics.Vector2.Zero,
        FieldLength + 2 * (BoundaryWidthGoalLine ?? BoundaryWidth),
        FieldWidth + 2 * BoundaryWidth);

    /// <summary>
    /// Goal width (distance between inner edges of goal posts) in mm.
    /// </summary>
    [ProtoMember(3, IsRequired = true)]
    public int GoalWidth { get; set; }

    /// <summary>
    /// Goal depth (distance from outer goal line edge to inner goal back) in mm.
    /// </summary>
    [ProtoMember(4, IsRequired = true)]
    public int GoalDepth { get; set; }

    /// <summary>
    /// Generated line segments based on the other parameters.
    /// </summary>
    [ProtoMember(6)]
    public List<FieldLineSegment> Lines { get; set; } = [];

    /// <summary>
    /// Generated circular arcs based on the other parameters.
    /// </summary>
    [ProtoMember(7)]
    public List<FieldCircularArc> Arcs { get; set; } = [];

    /// <summary>
    /// Depth of the penalty/defense area (measured between line centers) in mm.
    /// </summary>
    [ProtoMember(8)]
    public int? PenaltyAreaDepth { get; set; }

    /// <summary>
    /// Width of the penalty/defense area (measured between line centers) in mm.
    /// </summary>
    [ProtoMember(9)]
    public int? PenaltyAreaWidth { get; set; }

    /// <summary>
    /// Radius of the center circle (measured between line centers) in mm.
    /// </summary>
    [ProtoMember(10)]
    public int? CenterCircleRadius { get; set; }

    /// <summary>
    /// Circle representing the center circle of the field.
    /// </summary>
    public Circle CenterCircle => new()
        { Center = System.Numerics.Vector2.Zero, Radius = CenterCircleRadius.GetValueOrDefault() };

    /// <summary>
    /// Thickness/width of the lines on the field in mm.
    /// </summary>
    [ProtoMember(11)]
    public int? LineThickness { get; set; }

    /// <summary>
    /// Distance between the goal center and the center of the penalty mark in mm.
    /// </summary>
    [ProtoMember(12)]
    public int? GoalCenterToPenaltyMark { get; set; }

    /// <summary>
    /// Goal height in mm.
    /// </summary>
    [ProtoMember(13)]
    public int? GoalHeight { get; set; }

    /// <summary>
    /// Ball radius in mm (note that this is a float type to represent sub-mm precision).
    /// </summary>
    [ProtoMember(14)]
    public float? BallRadius { get; set; }

    /// <summary>
    /// Max allowed robot radius in mm (note that this is a float type to represent sub-mm precision).
    /// </summary>
    [ProtoMember(15)]
    public float? MaxRobotRadius { get; set; }

    // Compatibility alias for the old Tyr naming.
    public float? RobotRadius
    {
        get => MaxRobotRadius;
        set => MaxRobotRadius = value;
    }

    /// <summary>
    /// Boundary width at the goal lines (distance from goal line centers to boundary walls) in mm.
    /// </summary>
    [ProtoMember(16)]
    public int? BoundaryWidthGoalLine { get; set; }

    /// <summary>
    /// Width of the goal substitution area in mm.
    /// </summary>
    [ProtoMember(17)]
    public int? GoalSubstitutionAreaWidth { get; set; }

    public static readonly FieldSize DivisionA = new()
    {
        FieldLength = 12000,
        FieldWidth = 9000,
        GoalWidth = 1800,
        GoalDepth = 180,
        BoundaryWidth = 300,
        BoundaryWidthGoalLine = 600,
        PenaltyAreaDepth = 1800,
        PenaltyAreaWidth = 3600,
        CenterCircleRadius = 500,
        LineThickness = 10,
        GoalCenterToPenaltyMark = 8000,
        GoalHeight = 155,
        BallRadius = 21.5f,
        MaxRobotRadius = 90f,
        GoalSubstitutionAreaWidth = 300,
        // TODO: add lines / arcs
    };
}

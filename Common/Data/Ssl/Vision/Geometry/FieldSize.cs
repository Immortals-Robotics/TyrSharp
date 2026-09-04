using Tyr.Common.Config;
using ProtoBuf;
using Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Math.Shapes;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

/// <summary>
/// Contains the dimensions and markings of the SSL field.
/// </summary>
[ProtoContract]
[Configurable]
public partial struct FieldSize
{
    public FieldSize()
    {
    }

    [ConfigEntry("Division used to fill in missing optional SSL-Vision geometry fields")]
    public static partial Division DefaultDivision { get; set; } = Division.A;

    public static FieldSize Default => DefaultDivision switch
    {
        Division.B => DivisionB,
        _ => DivisionA
    };

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
        FieldLength + 2 * BoundaryWidthGoalLine,
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
    [ProtoMember(8, Name = "penalty_area_depth")]
    private int? PenaltyAreaDepthRaw { get; set; }

    public int PenaltyAreaDepth
    {
        get => PenaltyAreaDepthRaw ?? ResolvedDefaults.PenaltyAreaDepthRaw.GetValueOrDefault();
        set => PenaltyAreaDepthRaw = value;
    }

    /// <summary>
    /// Width of the penalty/defense area (measured between line centers) in mm.
    /// </summary>
    [ProtoMember(9, Name = "penalty_area_width")]
    private int? PenaltyAreaWidthRaw { get; set; }

    public int PenaltyAreaWidth
    {
        get => PenaltyAreaWidthRaw ?? ResolvedDefaults.PenaltyAreaWidthRaw.GetValueOrDefault();
        set => PenaltyAreaWidthRaw = value;
    }

    /// <summary>
    /// Radius of the center circle (measured between line centers) in mm.
    /// </summary>
    [ProtoMember(10, Name = "center_circle_radius")]
    private int? CenterCircleRadiusRaw { get; set; }

    public int CenterCircleRadius
    {
        get => CenterCircleRadiusRaw ?? ResolvedDefaults.CenterCircleRadiusRaw.GetValueOrDefault();
        set => CenterCircleRadiusRaw = value;
    }

    /// <summary>
    /// Circle representing the center circle of the field.
    /// </summary>
    public Circle CenterCircle => new()
        { Center = System.Numerics.Vector2.Zero, Radius = CenterCircleRadius };

    /// <summary>
    /// Thickness/width of the lines on the field in mm.
    /// </summary>
    [ProtoMember(11, Name = "line_thickness")]
    private int? LineThicknessRaw { get; set; }

    public int LineThickness
    {
        get => LineThicknessRaw ?? ResolvedDefaults.LineThicknessRaw.GetValueOrDefault();
        set => LineThicknessRaw = value;
    }

    /// <summary>
    /// Distance between the goal center and the center of the penalty mark in mm.
    /// </summary>
    [ProtoMember(12, Name = "goal_center_to_penalty_mark")]
    private int? GoalCenterToPenaltyMarkRaw { get; set; }

    public int GoalCenterToPenaltyMark
    {
        get => GoalCenterToPenaltyMarkRaw ?? ResolvedDefaults.GoalCenterToPenaltyMarkRaw.GetValueOrDefault();
        set => GoalCenterToPenaltyMarkRaw = value;
    }

    /// <summary>
    /// Goal height in mm.
    /// </summary>
    [ProtoMember(13, Name = "goal_height")]
    private int? GoalHeightRaw { get; set; }

    public int GoalHeight
    {
        get => GoalHeightRaw ?? ResolvedDefaults.GoalHeightRaw.GetValueOrDefault();
        set => GoalHeightRaw = value;
    }

    /// <summary>
    /// Ball radius in mm (note that this is a float type to represent sub-mm precision).
    /// </summary>
    [ProtoMember(14, Name = "ball_radius")]
    private float? BallRadiusRaw { get; set; }

    public float BallRadius
    {
        get => BallRadiusRaw ?? ResolvedDefaults.BallRadiusRaw.GetValueOrDefault();
        set => BallRadiusRaw = value;
    }

    /// <summary>
    /// Max allowed robot radius in mm (note that this is a float type to represent sub-mm precision).
    /// </summary>
    [ProtoMember(15, Name = "max_robot_radius")]
    private float? MaxRobotRadiusRaw { get; set; }

    public float MaxRobotRadius
    {
        get => MaxRobotRadiusRaw ?? ResolvedDefaults.MaxRobotRadiusRaw.GetValueOrDefault();
        set => MaxRobotRadiusRaw = value;
    }

    // Compatibility alias for the old Tyr naming.
    public float RobotRadius
    {
        get => MaxRobotRadius;
        set => MaxRobotRadius = value;
    }

    /// <summary>
    /// Boundary width at the goal lines (distance from goal line centers to boundary walls) in mm.
    /// </summary>
    [ProtoMember(16, Name = "boundary_width_goal_line")]
    private int? BoundaryWidthGoalLineRaw { get; set; }

    public int BoundaryWidthGoalLine
    {
        get => BoundaryWidthGoalLineRaw ?? ResolvedDefaults.BoundaryWidthGoalLineRaw ?? BoundaryWidth;
        set => BoundaryWidthGoalLineRaw = value;
    }

    /// <summary>
    /// Width of the goal substitution area in mm.
    /// </summary>
    [ProtoMember(17, Name = "goal_substitution_area_width")]
    private int? GoalSubstitutionAreaWidthRaw { get; set; }

    public int GoalSubstitutionAreaWidth
    {
        get => GoalSubstitutionAreaWidthRaw ?? ResolvedDefaults.GoalSubstitutionAreaWidthRaw.GetValueOrDefault();
        set => GoalSubstitutionAreaWidthRaw = value;
    }

    private FieldSize ResolvedDefaults => Default;

    public static readonly FieldSize DivisionA = new()
    {
        FieldLength = 12000,
        FieldWidth = 9000,
        GoalWidth = 1800,
        GoalDepth = 180,
        BoundaryWidth = 700,
        BoundaryWidthGoalLine = 700,
        PenaltyAreaDepth = 1800,
        PenaltyAreaWidth = 3600,
        CenterCircleRadius = 500,
        LineThickness = 10,
        GoalCenterToPenaltyMark = 8000,
        GoalHeight = 160,
        BallRadius = 21.5f,
        MaxRobotRadius = 90f,
        GoalSubstitutionAreaWidth = 300,
        // TODO: add lines / arcs
    };

    public static readonly FieldSize DivisionB = new()
    {
        FieldLength = 9000,
        FieldWidth = 6000,
        GoalWidth = 1000,
        GoalDepth = 180,
        BoundaryWidth = 700,
        BoundaryWidthGoalLine = 700,
        PenaltyAreaDepth = 1000,
        PenaltyAreaWidth = 2000,
        CenterCircleRadius = 500,
        LineThickness = 10,
        GoalCenterToPenaltyMark = 6000,
        GoalHeight = 160,
        BallRadius = 21.5f,
        MaxRobotRadius = 90f,
        GoalSubstitutionAreaWidth = 300,
        // TODO: add lines / arcs
    };
}

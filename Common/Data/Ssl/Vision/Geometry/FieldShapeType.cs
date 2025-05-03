namespace Tyr.Common.Data.Ssl.Vision.Geometry;

/// <summary>
/// Enumeration of field shape types used to identify different field markings.
/// </summary>
public enum FieldShapeType
{
    /// <summary>
    /// Undefined field shape type.
    /// </summary>
    Undefined = 0,
    
    /// <summary>
    /// The center circle of the field.
    /// </summary>
    CenterCircle = 1,
    
    /// <summary>
    /// The top touch line of the field.
    /// </summary>
    TopTouchLine = 2,
    
    /// <summary>
    /// The bottom touch line of the field.
    /// </summary>
    BottomTouchLine = 3,
    
    /// <summary>
    /// The left goal line of the field.
    /// </summary>
    LeftGoalLine = 4,
    
    /// <summary>
    /// The right goal line of the field.
    /// </summary>
    RightGoalLine = 5,
    
    /// <summary>
    /// The halfway line of the field.
    /// </summary>
    HalfwayLine = 6,
    
    /// <summary>
    /// The center line of the field.
    /// </summary>
    CenterLine = 7,
    
    /// <summary>
    /// The left penalty stretch of the field.
    /// </summary>
    LeftPenaltyStretch = 8,
    
    /// <summary>
    /// The right penalty stretch of the field.
    /// </summary>
    RightPenaltyStretch = 9,
    
    /// <summary>
    /// The left penalty stretch on the left side of the field.
    /// </summary>
    LeftFieldLeftPenaltyStretch = 10,
    
    /// <summary>
    /// The right penalty stretch on the left side of the field.
    /// </summary>
    LeftFieldRightPenaltyStretch = 11,
    
    /// <summary>
    /// The left penalty stretch on the right side of the field.
    /// </summary>
    RightFieldLeftPenaltyStretch = 12,
    
    /// <summary>
    /// The right penalty stretch on the right side of the field.
    /// </summary>
    RightFieldRightPenaltyStretch = 13
}
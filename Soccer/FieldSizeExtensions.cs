using System.Numerics;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Math.Shapes;

namespace Tyr.Soccer;

public static class FieldSizeExtensions
{
    public static Vector2 OwnGoal(this FieldSize field)
    {
        return new Vector2(Context.SideSign * field.Width, 0);
    }

    public static Vector2 OwnGoalPostTop(this FieldSize field)
    {
        return field.OwnGoal() + new Vector2(0, field.GoalWidth / 2.0f);
    }

    public static Vector2 OwnGoalPostBottom(this FieldSize field)
    {
        return field.OwnGoal() - new Vector2(0, field.GoalWidth / 2.0f);
    }

    public static LineSegment OwnGoalLine(this FieldSize field)
    {
        return new LineSegment() { Start = field.OwnGoalPostBottom(), End = field.OwnGoalPostTop() };
    }

    public static Vector2 OppGoal(this FieldSize field)
    {
        return new Vector2(-Context.SideSign * field.Width, 0);
    }

    public static Vector2 OppGoalPostTop(this FieldSize field)
    {
        return field.OppGoal() + new Vector2(0, field.GoalWidth / 2.0f);
    }

    public static Vector2 OppGoalPostBottom(this FieldSize field)
    {
        return field.OppGoal() - new Vector2(0, field.GoalWidth / 2.0f);
    }

    public static LineSegment OppGoalLine(this FieldSize field)
    {
        return new LineSegment() { Start = field.OppGoalPostBottom(), End = field.OppGoalPostTop() };
    }

    public static Rectangle OwnPenaltyArea(this FieldSize field)
    {
        float w = -Context.SideSign * field.PenaltyAreaDepth;
        float h = field.PenaltyAreaWidth;

        return Rectangle.FromCornerAndSize(new Vector2(Context.SideSign * field.Width, -h / 2), w, h);
    }

    public static Rectangle OppPenaltyArea(this FieldSize field)
    {
        float w = Context.SideSign * field.PenaltyAreaDepth;
        float h = field.PenaltyAreaWidth;

        return Rectangle.FromCornerAndSize(new Vector2(-Context.SideSign * field.Width, -h / 2), w, h);
    }

    public static Rectangle ExtendedOwnPenaltyArea(this FieldSize field, float extensionSize, float goalLineOffset = 0f)
    {
        float penaltyAreaHalfWidth = field.PenaltyAreaWidth / 2.0f;
        var start = new Vector2(field.OwnGoal().X - Context.SideSign * goalLineOffset, -(penaltyAreaHalfWidth + extensionSize));
        float w = -Context.SideSign * (field.PenaltyAreaDepth + extensionSize - goalLineOffset);
        float h = field.PenaltyAreaWidth + 2 * extensionSize;
        return Rectangle.FromCornerAndSize(start, w, h);
    }

    public static float OwnGoalSafeX(this FieldSize field, float margin = 20f)
    {
        return field.OwnGoal().X - (Context.SideSign * (field.RobotRadius + margin));
    }

    public static Rectangle OurHalf(this FieldSize field)
    {
        float h = field.Height + field.BoundaryWidth;
        return Rectangle.FromCornerAndSize(new Vector2(0, -h), Context.SideSign * field.Width, 2 * h);
    }

    public static Rectangle TheirHalf(this FieldSize field)
    {
        float h = field.Height + field.BoundaryWidth;
        return Rectangle.FromCornerAndSize(new Vector2(0, -h), -Context.SideSign * field.Width, 2 * h);
    }

    public static bool IsOut(this FieldSize field, Vector2 point, float margin = 0.0f)
    {
        return MathF.Abs(point.X) > field.Width + margin || MathF.Abs(point.Y) > field.Height + margin;
    }
}

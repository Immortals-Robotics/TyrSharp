using System.Runtime.CompilerServices;
using Tyr.Common.Dataflow;
using Tyr.Common.Math;
using Tyr.Common.Shape;

namespace Tyr.Common.Debug;

public class Drawer(string category)
{
    private void Draw<T>(T shape, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
        where T : struct, IShape
    {
        var command = new DrawCommand<T>(
            shape, color, options,
            category, DateTime.UtcNow, memberName, filePath, lineNumber);

        Hub.Draws<T>().Publish(command);
    }

    public void DrawPoint(Vector2 position, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
    }

    public void DrawLine(Vector2 start, Vector2 end, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawLine(Line.FromTwoPoints(start, end), color, options, memberName, filePath, lineNumber);
    }

    public void DrawLine(Line line, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
    }

    public void DrawLineSegment(Vector2 start, Vector2 end, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawLineSegment(new LineSegment(start, end), color, options, memberName, filePath, lineNumber);
    }

    public void DrawLineSegment(LineSegment segment, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
    }

    public void DrawArrow(Vector2 start, Vector2 end, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawArrow(new LineSegment(start, end), color, options, memberName, filePath, lineNumber);
    }

    public void DrawArrow(LineSegment segment, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
    }

    public void DrawRectangle(Vector2 min, Vector2 max, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawRectangle(new Rect(min, max), color, options, memberName, filePath, lineNumber);
    }

    public void DrawRectangle(Rect rect, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
    }

    public void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawTriangle(new Triangle(v1, v2, v3), color, options, memberName, filePath, lineNumber);
    }

    public void DrawTriangle(Triangle triangle, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
    }

    public void DrawRobot(Robot robot, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
    }

    public void DrawPath(IReadOnlyList<Vector2> points, Color color, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
    }

    public void DrawText(string text, Vector2 position, Color color, float size, DrawOptions options,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
    }
}
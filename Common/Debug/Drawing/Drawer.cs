using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Tyr.Common.Dataflow;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Math;
using Circle = Tyr.Common.Debug.Drawing.Drawables.Circle;
using Line = Tyr.Common.Debug.Drawing.Drawables.Line;
using LineSegment = Tyr.Common.Debug.Drawing.Drawables.LineSegment;
using Robot = Tyr.Common.Debug.Drawing.Drawables.Robot;
using Triangle = Tyr.Common.Debug.Drawing.Drawables.Triangle;

namespace Tyr.Common.Debug.Drawing;

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public class Drawer(string moduleName)
{
    private void Draw(IDrawable drawable, Color color, Options options,
        string? memberName, string? filePath, int lineNumber)
    {
        var timestamp = Timestamp.Now;
        var meta = new Meta(moduleName, timestamp, memberName, filePath, lineNumber);
        var command = new Command(drawable, color, options, meta);

        Hub.Draws.Publish(command);
    }

    public void DrawPoint(Vector2 position, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var point = new Point(position);
        Draw(point, color, options, memberName, filePath, lineNumber);
    }

    public void DrawLine(Vector2 point, Angle angle, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var line = new Line(point, angle);
        Draw(line, color, options, memberName, filePath, lineNumber);
    }

    public void DrawLine(Math.Shapes.Line line, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawLine(line.SomePoint, line.Angle, color, options, memberName, filePath, lineNumber);
    }

    public void DrawLineSegment(Vector2 start, Vector2 end, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var segment = new LineSegment(start, end);
        Draw(segment, color, options, memberName, filePath, lineNumber);
    }

    public void DrawLineSegment(Math.Shapes.LineSegment segment, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawLineSegment(segment.Start, segment.End, color, options, memberName, filePath, lineNumber);
    }

    public void DrawArrow(Vector2 start, Vector2 end, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var arrow = new Arrow(start, end);
        Draw(arrow, color, options, memberName, filePath, lineNumber);
    }

    public void DrawArrow(Math.Shapes.LineSegment segment, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawArrow(segment.Start, segment.End, color, options, memberName, filePath, lineNumber);
    }

    public void DrawRectangle(Vector2 min, Vector2 max, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var rect = new Rectangle(min, max);
        Draw(rect, color, options, memberName, filePath, lineNumber);
    }

    public void DrawRectangle(Math.Shapes.Rectangle rectangle, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawRectangle(rectangle.Min, rectangle.Max, color, options, memberName, filePath, lineNumber);
    }

    public void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var triangle = new Triangle(v1, v2, v3);
        Draw(triangle, color, options, memberName, filePath, lineNumber);
    }

    public void DrawTriangle(Math.Shapes.Triangle triangle, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawTriangle(triangle.Corner1, triangle.Corner2, triangle.Corner3,
            color, options, memberName, filePath, lineNumber);
    }

    public void DrawCircle(Vector2 center, float radius, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var circle = new Circle(center, radius);
        Draw(circle, color, options, memberName, filePath, lineNumber);
    }

    public void DrawCircle(Math.Shapes.Circle circle, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawCircle(circle.Center, circle.Radius, color, options, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Vector2 position, Angle? orientation, int? id, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var robot = new Robot(position, orientation, id);
        Draw(robot, color, options, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Math.Shapes.Robot robot, int? id, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        DrawRobot(robot.Center, robot.Angle, id, color, options, memberName, filePath, lineNumber);
    }

    public void DrawPath(IReadOnlyList<Vector2> points, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var path = new Drawables.Path(points);
        Draw(path, color, options, memberName, filePath, lineNumber);
    }

    public void DrawText(string content, Vector2 position, float size, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var text = new Text(content, position, size);
        Draw(text, color, options, memberName, filePath, lineNumber);
    }
}
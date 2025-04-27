using System.Runtime.CompilerServices;
using Tyr.Common.Data;
using Tyr.Common.Dataflow;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Math;
using Circle = Tyr.Common.Debug.Drawing.Drawables.Circle;
using Line = Tyr.Common.Debug.Drawing.Drawables.Line;
using LineSegment = Tyr.Common.Debug.Drawing.Drawables.LineSegment;
using Robot = Tyr.Common.Debug.Drawing.Drawables.Robot;
using Triangle = Tyr.Common.Debug.Drawing.Drawables.Triangle;
using Vector2 = System.Numerics.Vector2;

namespace Tyr.Common.Debug.Drawing;

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

    public void DrawEmpty(
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var empty = new Empty();
        Draw(empty, Color.Black, default, memberName, filePath, lineNumber);
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
        var drawable = new Line(line);
        Draw(drawable, color, options, memberName, filePath, lineNumber);
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
        var drawable = new LineSegment(segment);
        Draw(drawable, color, options, memberName, filePath, lineNumber);
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
        var drawable = new Arrow(segment);
        Draw(drawable, color, options, memberName, filePath, lineNumber);
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
        var drawable = new Rectangle(rectangle);
        Draw(drawable, color, options, memberName, filePath, lineNumber);
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
        var drawable = new Triangle(triangle);
        Draw(drawable, color, options, memberName, filePath, lineNumber);
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
        var drawable = new Circle(circle);
        Draw(drawable, color, options, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Vector2 position, Angle? orientation, uint? id, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var robot = new Robot(position, orientation, id);
        Draw(robot, color, options, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Math.Shapes.Robot robot, uint? id, Color color, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Robot(robot, id);
        Draw(drawable, color, options, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Data.Ssl.Vision.Detection.Robot robot, Data.Ssl.RobotId id, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Robot(robot, id.Id);
        var color = id.Team.ToColor();

        Draw(drawable, color, options, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Data.Ssl.Vision.Tracker.Robot robot, Options options = default,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Robot(robot);
        var color = robot.Id.Team.ToColor();

        Draw(drawable, color, options, memberName, filePath, lineNumber);
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
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
        string? expression, string? memberName, string? filePath, int lineNumber)
    {
        var timestamp = Timestamp.Now;
        var meta = new Meta(moduleName, timestamp, expression, memberName, filePath, lineNumber);
        var command = new Command(drawable, color, options, meta);

        Hub.Draws.Publish(command);
    }

    public void DrawEmpty(
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var empty = new Empty();
        Draw(empty, Color.Black, default, "", memberName, filePath, lineNumber);
    }

    public void DrawPoint(Vector2 position, Color color, Options options = default,
        [CallerArgumentExpression("position")] string? positionExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var point = new Point(position);
        Draw(point, color, options, positionExpression, memberName, filePath, lineNumber);
    }

    public void DrawLine(Vector2 point, Angle angle, Color color, Options options = default,
        [CallerArgumentExpression("point")] string? pointExpression = null,
        [CallerArgumentExpression("angle")] string? angleExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var line = new Line(point, angle);
        var expression = $"point: {pointExpression}, angle: {angleExpression}";
        Draw(line, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawLine(Math.Shapes.Line line, Color color, Options options = default,
        [CallerArgumentExpression("line")] string? lineExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Line(line);
        Draw(drawable, color, options, lineExpression, memberName, filePath, lineNumber);
    }

    public void DrawLineSegment(Vector2 start, Vector2 end, Color color, Options options = default,
        [CallerArgumentExpression("start")] string? startExpression = null,
        [CallerArgumentExpression("end")] string? endExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var segment = new LineSegment(start, end);
        var expression = $"start: {startExpression}, end: {endExpression}";
        Draw(segment, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawLineSegment(Math.Shapes.LineSegment segment, Color color, Options options = default,
        [CallerArgumentExpression("segment")] string? segmentExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new LineSegment(segment);
        Draw(drawable, color, options, segmentExpression, memberName, filePath, lineNumber);
    }

    public void DrawArrow(Vector2 start, Vector2 end, Color color, Options options = default,
        [CallerArgumentExpression("start")] string? startExpression = null,
        [CallerArgumentExpression("end")] string? endExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var arrow = new Arrow(start, end);
        var expression = $"start: {startExpression}, end: {endExpression}";
        Draw(arrow, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawArrow(Math.Shapes.LineSegment segment, Color color, Options options = default,
        [CallerArgumentExpression("segment")] string? segmentExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Arrow(segment);
        Draw(drawable, color, options, segmentExpression, memberName, filePath, lineNumber);
    }

    public void DrawRectangle(Vector2 min, Vector2 max, Color color, Options options = default,
        [CallerArgumentExpression("min")] string? minExpression = null,
        [CallerArgumentExpression("max")] string? maxExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var rect = new Rectangle(min, max);
        var expression = $"min: {minExpression}, max: {maxExpression}";
        Draw(rect, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawRectangle(Math.Shapes.Rectangle rectangle, Color color, Options options = default,
        [CallerArgumentExpression("rectangle")]
        string? rectangleExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Rectangle(rectangle);
        Draw(drawable, color, options, rectangleExpression, memberName, filePath, lineNumber);
    }

    public void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color, Options options = default,
        [CallerArgumentExpression("v1")] string? v1Expression = null,
        [CallerArgumentExpression("v2")] string? v2Expression = null,
        [CallerArgumentExpression("v3")] string? v3Expression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var triangle = new Triangle(v1, v2, v3);
        var expression = $"v1: {v1Expression}, v2: {v2Expression}, v3: {v3Expression}";
        Draw(triangle, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawTriangle(Math.Shapes.Triangle triangle, Color color, Options options = default,
        [CallerArgumentExpression("triangle")] string? triangleExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Triangle(triangle);
        Draw(drawable, color, options, triangleExpression, memberName, filePath, lineNumber);
    }

    public void DrawCircle(Vector2 center, float radius, Color color, Options options = default,
        [CallerArgumentExpression("center")] string? centerExpression = null,
        [CallerArgumentExpression("radius")] string? radiusExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var circle = new Circle(center, radius);
        var expression = $"center: {centerExpression}, radius: {radiusExpression}";
        Draw(circle, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawCircle(Math.Shapes.Circle circle, Color color, Options options = default,
        [CallerArgumentExpression("circle")] string? circleExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Circle(circle);
        Draw(drawable, color, options, circleExpression, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Vector2 position, Angle? orientation, uint? id, Color color, Options options = default,
        [CallerArgumentExpression("position")] string? positionExpression = null,
        [CallerArgumentExpression("orientation")]
        string? orientationExpression = null,
        [CallerArgumentExpression("id")] string? idExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var robot = new Robot(position, orientation, id);
        var expression = $"position: {positionExpression}, orientation: {orientationExpression}, id: {idExpression}";
        Draw(robot, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Math.Shapes.Robot robot, uint? id, Color color, Options options = default,
        [CallerArgumentExpression("robot")] string? robotExpression = null,
        [CallerArgumentExpression("id")] string? idExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Robot(robot, id);
        var expression = $"robot: {robotExpression}, id: {idExpression}";
        Draw(drawable, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Data.Ssl.Vision.Detection.Robot robot, Data.Ssl.RobotId id, Options options = default,
        [CallerArgumentExpression("robot")] string? robotExpression = null,
        [CallerArgumentExpression("id")] string? idExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Robot(robot, id.Id);
        var color = id.Team.ToColor();
        var expression = $"robot: {robotExpression}, id: {idExpression}";
        Draw(drawable, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Data.Ssl.Vision.Tracker.Robot robot, Options options = default,
        [CallerArgumentExpression("robot")] string? robotExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Robot(robot);
        var color = robot.Id.Team.ToColor();
        Draw(drawable, color, options, robotExpression, memberName, filePath, lineNumber);
    }

    public void DrawPath(IReadOnlyList<Vector2> points, Color color, Options options = default,
        [CallerArgumentExpression("points")] string? pointsExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var path = new Drawables.Path(points);
        Draw(path, color, options, pointsExpression, memberName, filePath, lineNumber);
    }

    public void DrawText(string content, Vector2 position, float size, Color color, Options options = default,
        [CallerArgumentExpression("content")] string? contentExpression = null,
        [CallerArgumentExpression("position")] string? positionExpression = null,
        [CallerArgumentExpression("size")] string? sizeExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var text = new Text(content, position, size);
        var expression = $"content: {contentExpression}, position: {positionExpression}, size: {sizeExpression}";
        Draw(text, color, options, expression, memberName, filePath, lineNumber);
    }
}
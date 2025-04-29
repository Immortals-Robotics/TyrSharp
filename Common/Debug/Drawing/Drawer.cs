using System.Runtime.CompilerServices;
using System.Text;
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
    private readonly StringBuilder _stringBuilder = new();

    private void Draw(IDrawable drawable, Color color, Options options,
        string? expression, string? memberName, string? filePath, int lineNumber)
    {
        var meta = Meta.GetOrCreate(moduleName, expression, memberName, filePath, lineNumber);
        var command = new Command(drawable, color, options, meta, Timestamp.Now);

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
        [CallerArgumentExpression(nameof(position))]
        string? positionExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var point = new Point(position);
        Draw(point, color, options, positionExpression, memberName, filePath, lineNumber);
    }

    public void DrawLine(Vector2 point, Angle angle, Color color, Options options = default,
        [CallerArgumentExpression(nameof(point))]
        string? pointExpression = null,
        [CallerArgumentExpression(nameof(angle))]
        string? angleExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var line = new Line(point, angle);

        _stringBuilder.Clear()
            .Append(nameof(point)).Append(": ").Append(pointExpression)
            .Append(", ")
            .Append(nameof(angle)).Append(": ").Append(angleExpression);
        var expression = string.Intern(_stringBuilder.ToString());

        Draw(line, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawLine(Math.Shapes.Line line, Color color, Options options = default,
        [CallerArgumentExpression(nameof(line))]
        string? lineExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Line(line);
        Draw(drawable, color, options, lineExpression, memberName, filePath, lineNumber);
    }

    public void DrawLineSegment(Vector2 start, Vector2 end, Color color, Options options = default,
        [CallerArgumentExpression(nameof(start))]
        string? startExpression = null,
        [CallerArgumentExpression(nameof(end))]
        string? endExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var segment = new LineSegment(start, end);

        _stringBuilder.Clear()
            .Append(nameof(start)).Append(": ").Append(startExpression)
            .Append(", ")
            .Append(nameof(end)).Append(": ").Append(endExpression);
        var expression = string.Intern(_stringBuilder.ToString());

        Draw(segment, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawLineSegment(Math.Shapes.LineSegment segment, Color color, Options options = default,
        [CallerArgumentExpression(nameof(segment))]
        string? segmentExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new LineSegment(segment);
        Draw(drawable, color, options, segmentExpression, memberName, filePath, lineNumber);
    }

    public void DrawArrow(Vector2 start, Vector2 end, Color color, Options options = default,
        [CallerArgumentExpression(nameof(start))]
        string? startExpression = null,
        [CallerArgumentExpression(nameof(end))]
        string? endExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var arrow = new Arrow(start, end);

        _stringBuilder.Clear()
            .Append(nameof(start)).Append(": ").Append(startExpression)
            .Append(", ")
            .Append(nameof(end)).Append(": ").Append(endExpression);
        var expression = string.Intern(_stringBuilder.ToString());

        Draw(arrow, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawArrow(Math.Shapes.LineSegment segment, Color color, Options options = default,
        [CallerArgumentExpression(nameof(segment))]
        string? segmentExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Arrow(segment);
        Draw(drawable, color, options, segmentExpression, memberName, filePath, lineNumber);
    }

    public void DrawRectangle(Vector2 min, Vector2 max, Color color, Options options = default,
        [CallerArgumentExpression(nameof(min))]
        string? minExpression = null,
        [CallerArgumentExpression(nameof(max))]
        string? maxExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var rect = new Rectangle(min, max);

        _stringBuilder.Clear()
            .Append(nameof(min)).Append(": ").Append(minExpression)
            .Append(", ")
            .Append(nameof(max)).Append(": ").Append(maxExpression);
        var expression = string.Intern(_stringBuilder.ToString());

        Draw(rect, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawRectangle(Math.Shapes.Rectangle rectangle, Color color, Options options = default,
        [CallerArgumentExpression(nameof(rectangle))]
        string? rectangleExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Rectangle(rectangle);
        Draw(drawable, color, options, rectangleExpression, memberName, filePath, lineNumber);
    }

    public void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color, Options options = default,
        [CallerArgumentExpression(nameof(v1))] string? v1Expression = null,
        [CallerArgumentExpression(nameof(v2))] string? v2Expression = null,
        [CallerArgumentExpression(nameof(v3))] string? v3Expression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var triangle = new Triangle(v1, v2, v3);

        _stringBuilder.Clear()
            .Append(nameof(v1)).Append(": ").Append(v1Expression)
            .Append(", ")
            .Append(nameof(v2)).Append(": ").Append(v2Expression)
            .Append(", ")
            .Append(nameof(v3)).Append(": ").Append(v3Expression);
        var expression = string.Intern(_stringBuilder.ToString());

        Draw(triangle, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawTriangle(Math.Shapes.Triangle triangle, Color color, Options options = default,
        [CallerArgumentExpression(nameof(triangle))]
        string? triangleExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Triangle(triangle);
        Draw(drawable, color, options, triangleExpression, memberName, filePath, lineNumber);
    }

    public void DrawCircle(Vector2 center, float radius, Color color, Options options = default,
        [CallerArgumentExpression(nameof(center))]
        string? centerExpression = null,
        [CallerArgumentExpression(nameof(radius))]
        string? radiusExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var circle = new Circle(center, radius);

        _stringBuilder.Clear()
            .Append(nameof(center)).Append(": ").Append(centerExpression)
            .Append(", ")
            .Append(nameof(radius)).Append(": ").Append(radiusExpression);
        var expression = string.Intern(_stringBuilder.ToString());

        Draw(circle, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawCircle(Math.Shapes.Circle circle, Color color, Options options = default,
        [CallerArgumentExpression(nameof(circle))]
        string? circleExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Circle(circle);
        Draw(drawable, color, options, circleExpression, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Vector2 position, Angle? orientation, uint? id, Color color, Options options = default,
        [CallerArgumentExpression(nameof(position))]
        string? positionExpression = null,
        [CallerArgumentExpression(nameof(orientation))]
        string? orientationExpression = null,
        [CallerArgumentExpression(nameof(id))] string? idExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var robot = new Robot(position, orientation, id);

        _stringBuilder.Clear()
            .Append(nameof(position)).Append(": ").Append(positionExpression)
            .Append(", ")
            .Append(nameof(orientation)).Append(": ").Append(orientationExpression)
            .Append(", ")
            .Append(nameof(id)).Append(": ").Append(idExpression);
        var expression = string.Intern(_stringBuilder.ToString());

        Draw(robot, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Math.Shapes.Robot robot, uint? id, Color color, Options options = default,
        [CallerArgumentExpression(nameof(robot))]
        string? robotExpression = null,
        [CallerArgumentExpression(nameof(id))] string? idExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Robot(robot, id);

        _stringBuilder.Clear()
            .Append(nameof(robot)).Append(": ").Append(robotExpression)
            .Append(", ")
            .Append(nameof(id)).Append(": ").Append(idExpression);
        var expression = string.Intern(_stringBuilder.ToString());

        Draw(drawable, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Data.Ssl.Vision.Detection.Robot robot, Data.Ssl.RobotId id, Options options = default,
        [CallerArgumentExpression(nameof(robot))]
        string? robotExpression = null,
        [CallerArgumentExpression(nameof(id))] string? idExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Robot(robot, id.Id);
        var color = id.Team.ToColor();

        _stringBuilder.Clear()
            .Append(nameof(robot)).Append(": ").Append(robotExpression)
            .Append(", ")
            .Append(nameof(id)).Append(": ").Append(idExpression);
        var expression = string.Intern(_stringBuilder.ToString());

        Draw(drawable, color, options, expression, memberName, filePath, lineNumber);
    }

    public void DrawRobot(Data.Ssl.Vision.Tracker.Robot robot, Options options = default,
        [CallerArgumentExpression(nameof(robot))]
        string? robotExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Robot(robot);
        var color = robot.Id.Team.ToColor();
        Draw(drawable, color, options, robotExpression, memberName, filePath, lineNumber);
    }

    public void DrawPath(IReadOnlyList<Vector2> points, Color color, Options options = default,
        [CallerArgumentExpression(nameof(points))]
        string? pointsExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var path = new Drawables.Path(points);
        Draw(path, color, options, pointsExpression, memberName, filePath, lineNumber);
    }

    public void DrawText(string content, Vector2 position, float size, Color color, Options options = default,
        [CallerArgumentExpression(nameof(content))]
        string? contentExpression = null,
        [CallerArgumentExpression(nameof(position))]
        string? positionExpression = null,
        [CallerArgumentExpression(nameof(size))]
        string? sizeExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var text = new Text(content, position, size);

        _stringBuilder.Clear()
            .Append(nameof(content)).Append(": ").Append(contentExpression)
            .Append(", ")
            .Append(nameof(position)).Append(": ").Append(positionExpression)
            .Append(", ")
            .Append(nameof(size)).Append(": ").Append(sizeExpression);
        var expression = string.Intern(_stringBuilder.ToString());

        Draw(text, color, options, expression, memberName, filePath, lineNumber);
    }
}
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Cysharp.Text;
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

public sealed class Drawer(string moduleName) : IDisposable
{
    private Utf16ValueStringBuilder _stringBuilder = ZString.CreateStringBuilder();
    private readonly ConcurrentDictionary<int, string> _expressionCache = new();

    private string InternExpression(ReadOnlySpan<char> span)
    {
        var hashCode = string.GetHashCode(span);

        return _expressionCache.TryGetValue(hashCode, out var existing)
            ? existing
            : _expressionCache.GetOrAdd(hashCode, string.Intern(new string(span)));
    }

    private string MakeExpression(
        string name1, string? expression1,
        string name2, string? expression2)
    {
        _stringBuilder.Clear();
        _stringBuilder.AppendFormat("{0}: {1}, {2}: {3}",
            name1, expression1,
            name2, expression2);
        return InternExpression(_stringBuilder.AsSpan());
    }

    private string MakeExpression(
        string name1, string? expression1,
        string name2, string? expression2,
        string name3, string? expression3)
    {
        _stringBuilder.Clear();
        _stringBuilder.AppendFormat("{0}: {1}, {2}: {3}, {4}: {5}",
            name1, expression1,
            name2, expression2,
            name3, expression3);
        return InternExpression(_stringBuilder.AsSpan());
    }

    private void Draw(IDrawable drawable, Color color, Options options,
        string? expression, string? memberName, string? filePath, int lineNumber)
    {
        var meta = Meta.GetOrCreate(moduleName, expression, memberName, filePath, lineNumber);
        var command = new Command(drawable, color, options, meta, Timestamp.Now);

        Hub.Draws.Publish(command);
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

        var expression = MakeExpression(
            nameof(point), pointExpression,
            nameof(angle), angleExpression);
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

        var expression = MakeExpression(
            nameof(start), startExpression,
            nameof(end), endExpression);
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

        var expression = MakeExpression(
            nameof(start), startExpression,
            nameof(end), endExpression);

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

        var expression = MakeExpression(
            nameof(min), minExpression,
            nameof(max), maxExpression);

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

        var expression = MakeExpression(
            nameof(v1), v1Expression,
            nameof(v2), v2Expression,
            nameof(v3), v3Expression);

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

        var expression = MakeExpression(
            nameof(center), centerExpression,
            nameof(radius), radiusExpression);

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

        var expression = MakeExpression(
            nameof(position), positionExpression,
            nameof(orientation), orientationExpression,
            nameof(id), idExpression);

        Draw(robot, color, options, expression, memberName, filePath, lineNumber);
    }
    
    public void DrawRobot(Vector2 position, Angle? orientation, Data.Ssl.RobotId? id, Options options = default,
        [CallerArgumentExpression(nameof(position))]
        string? positionExpression = null,
        [CallerArgumentExpression(nameof(orientation))]
        string? orientationExpression = null,
        [CallerArgumentExpression(nameof(id))] string? idExpression = null,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var robot = new Robot(position, orientation, id?.Id);

        var expression = MakeExpression(
            nameof(position), positionExpression,
            nameof(orientation), orientationExpression,
            nameof(id), idExpression);
        
        var color = (id?.Team).ToColor();
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

        var expression = MakeExpression(
            nameof(robot), robotExpression,
            nameof(id), idExpression);

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

        var expression = MakeExpression(
            nameof(robot), robotExpression,
            nameof(id), idExpression);

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

        var expression = MakeExpression(
            nameof(content), contentExpression,
            nameof(position), positionExpression,
            nameof(size), sizeExpression);

        Draw(text, color, options, expression, memberName, filePath, lineNumber);
    }

    public void Dispose()
    {
        _stringBuilder.Dispose();
    }
}
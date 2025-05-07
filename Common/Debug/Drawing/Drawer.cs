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

public sealed class Drawer(string module) : IDisposable
{
    private string? _layer = null;

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

    private string MakeExpression(
        string name1, string? expression1,
        string name2, string? expression2,
        string name3, string? expression3,
        string name4, string? expression4,
        string name5, string? expression5)
    {
        _stringBuilder.Clear();
        _stringBuilder.AppendFormat("{0}: {1}, {2}: {3}, {4}: {5}, {6}: {7}, {8}: {9}",
            name1, expression1,
            name2, expression2,
            name3, expression3,
            name4, expression4,
            name5, expression5);
        return InternExpression(_stringBuilder.AsSpan());
    }

    private void Draw(IDrawable drawable, Color color, Options options,
        string? member, string? file, int line,
        string? expression, string? layer)
    {
        var meta = Meta.GetOrCreate(module, layer, file, member, line, expression);
        var command = new Command(drawable, color, options, meta, Timestamp.Now);

        Hub.Draws.Publish(command);
    }

    public void BeginLayer(string layer)
    {
        _layer = layer;
    }

    public void EndLayer()
    {
        _layer = null;
    }

    public void DrawPoint(Vector2 position, Color color, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(position))]
        string? positionExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var point = new Point(position);
        Draw(point, color, options, member, file, line, positionExpression, layer ?? _layer);
    }

    public void DrawLine(Vector2 point, Angle angle, Color color, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(point))]
        string? pointExpression = null,
        [CallerArgumentExpression(nameof(angle))]
        string? angleExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var line = new Line(point, angle);

        var expression = MakeExpression(
            nameof(point), pointExpression,
            nameof(angle), angleExpression);
        Draw(line, color, options, member, file, lineNumber, expression, layer ?? _layer);
    }

    public void DrawLine(Math.Shapes.Line line, Color color, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(line))]
        string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        var drawable = new Line(line);
        Draw(drawable, color, options, member, file, lineNumber, expression, layer ?? _layer);
    }

    public void DrawLineSegment(Vector2 start, Vector2 end, Color color, Options options = default,
        string? layer = null,
        [CallerArgumentExpression(nameof(start))]
        string? startExpression = null,
        [CallerArgumentExpression(nameof(end))]
        string? endExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var segment = new LineSegment(start, end);

        var expression = MakeExpression(
            nameof(start), startExpression,
            nameof(end), endExpression);
        Draw(segment, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawLineSegment(Math.Shapes.LineSegment segment, Color color, Options options = default,
        string? layer = null,
        [CallerArgumentExpression(nameof(segment))]
        string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var drawable = new LineSegment(segment);
        Draw(drawable, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawArrow(Vector2 start, Vector2 end, Color color, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(start))]
        string? startExpression = null,
        [CallerArgumentExpression(nameof(end))]
        string? endExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var arrow = new Arrow(start, end);

        var expression = MakeExpression(
            nameof(start), startExpression,
            nameof(end), endExpression);

        Draw(arrow, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawArrow(Math.Shapes.LineSegment segment, Color color, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(segment))]
        string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var drawable = new Arrow(segment);
        Draw(drawable, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawRectangle(Vector2 min, Vector2 max, Color color, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(min))]
        string? minExpression = null,
        [CallerArgumentExpression(nameof(max))]
        string? maxExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var rect = new Rectangle(min, max);

        var expression = MakeExpression(
            nameof(min), minExpression,
            nameof(max), maxExpression);

        Draw(rect, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawRectangle(Math.Shapes.Rectangle rectangle, Color color, Options options = default,
        string? layer = null,
        [CallerArgumentExpression(nameof(rectangle))]
        string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var drawable = new Rectangle(rectangle);
        Draw(drawable, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color, Options options = default,
        string? layer = null,
        [CallerArgumentExpression(nameof(v1))] string? v1Expression = null,
        [CallerArgumentExpression(nameof(v2))] string? v2Expression = null,
        [CallerArgumentExpression(nameof(v3))] string? v3Expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var triangle = new Triangle(v1, v2, v3);

        var expression = MakeExpression(
            nameof(v1), v1Expression,
            nameof(v2), v2Expression,
            nameof(v3), v3Expression);

        Draw(triangle, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawTriangle(Math.Shapes.Triangle triangle, Color color, Options options = default,
        string? layer = null,
        [CallerArgumentExpression(nameof(triangle))]
        string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var drawable = new Triangle(triangle);
        Draw(drawable, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawCircle(Vector2 center, float radius, Color color, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(center))]
        string? centerExpression = null,
        [CallerArgumentExpression(nameof(radius))]
        string? radiusExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var circle = new Circle(center, radius);

        var expression = MakeExpression(
            nameof(center), centerExpression,
            nameof(radius), radiusExpression);

        Draw(circle, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawCircle(Math.Shapes.Circle circle, Color color, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(circle))]
        string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var drawable = new Circle(circle);
        Draw(drawable, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawArc(Vector2 center, float radius, Angle start, Angle end, bool closed,
        Color color, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(center))]
        string? centerExpression = null,
        [CallerArgumentExpression(nameof(radius))]
        string? radiusExpression = null,
        [CallerArgumentExpression(nameof(start))]
        string? startExpression = null,
        [CallerArgumentExpression(nameof(end))]
        string? endExpression = null,
        [CallerArgumentExpression(nameof(closed))]
        string? closedExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var arc = new Arc(center, radius, start, end, closed);

        var expression = MakeExpression(
            nameof(center), centerExpression,
            nameof(radius), radiusExpression,
            nameof(start), startExpression,
            nameof(end), endExpression,
            nameof(closed), closedExpression);

        Draw(arc, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawRobot(Vector2 position, Angle? orientation, uint? id, Color color, float? radius = null,
        Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(position))]
        string? positionExpression = null,
        [CallerArgumentExpression(nameof(orientation))]
        string? orientationExpression = null,
        [CallerArgumentExpression(nameof(id))] string? idExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var robot = new Robot(position, orientation, id, radius);

        var expression = MakeExpression(
            nameof(position), positionExpression,
            nameof(orientation), orientationExpression,
            nameof(id), idExpression);

        Draw(robot, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawRobot(Vector2 position, Angle? orientation, Data.Ssl.RobotId? id, float? radius = null,
        Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(position))]
        string? positionExpression = null,
        [CallerArgumentExpression(nameof(orientation))]
        string? orientationExpression = null,
        [CallerArgumentExpression(nameof(id))] string? idExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var robot = new Robot(position, orientation, id?.Id, radius);

        var expression = MakeExpression(
            nameof(position), positionExpression,
            nameof(orientation), orientationExpression,
            nameof(id), idExpression);

        var color = (id?.Team).ToColor();
        Draw(robot, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawRobot(Math.Shapes.Robot robot, uint? id, Color color, Options options = default,
        string? layer = null,
        [CallerArgumentExpression(nameof(robot))]
        string? robotExpression = null,
        [CallerArgumentExpression(nameof(id))] string? idExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var drawable = new Robot(robot, id);

        var expression = MakeExpression(
            nameof(robot), robotExpression,
            nameof(id), idExpression);

        Draw(drawable, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawRobot(Data.Ssl.Vision.Detection.Robot robot, Data.Ssl.RobotId id, Options options = default,
        string? layer = null,
        [CallerArgumentExpression(nameof(robot))]
        string? robotExpression = null,
        [CallerArgumentExpression(nameof(id))] string? idExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var drawable = new Robot(robot, id.Id);
        var color = id.Team.ToColor();

        var expression = MakeExpression(
            nameof(robot), robotExpression,
            nameof(id), idExpression);

        Draw(drawable, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawRobot(Data.Ssl.Vision.Tracker.Robot robot, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(robot))]
        string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var drawable = new Robot(robot);
        var color = robot.Id.Team.ToColor();
        Draw(drawable, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawPath(IReadOnlyList<Vector2> points, Color color, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(points))]
        string? expression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var path = new Drawables.Path(points);
        Draw(path, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void DrawText(string content, Vector2 position, float size, Color color,
        TextAlignment alignment = TextAlignment.Center, Options options = default, string? layer = null,
        [CallerArgumentExpression(nameof(content))]
        string? contentExpression = null,
        [CallerArgumentExpression(nameof(position))]
        string? positionExpression = null,
        [CallerArgumentExpression(nameof(size))]
        string? sizeExpression = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var text = new Text(content, position, size, alignment);

        var expression = MakeExpression(
            nameof(content), contentExpression,
            nameof(position), positionExpression,
            nameof(size), sizeExpression);

        Draw(text, color, options, member, file, line, expression, layer ?? _layer);
    }

    public void Dispose()
    {
        _stringBuilder.Dispose();
    }
}
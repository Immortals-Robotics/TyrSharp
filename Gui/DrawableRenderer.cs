using System.Numerics;
using Hexa.NET.ImGui;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Color = Tyr.Common.Debug.Drawing.Color;
using Path = Tyr.Common.Debug.Drawing.Drawables.Path;
using Rectangle = Tyr.Common.Debug.Drawing.Drawables.Rectangle;
using Triangle = Tyr.Common.Debug.Drawing.Drawables.Triangle;

namespace Tyr.Gui;

internal class DrawableRenderer
{
    public Camera2D Camera { get; set; } = new();

    private ImDrawListPtr _drawList;

    internal void Draw(Command command)
    {
        _drawList = ImGui.GetWindowDrawList();

        switch (command.Drawable)
        {
            case Arrow arrow:
                DrawArrow(arrow, command.Color, command.Options);
                break;
            case Circle circle:
                DrawCircle(circle, command.Color, command.Options);
                break;
            case Line line:
                DrawLine(line, command.Color, command.Options);
                break;
            case LineSegment segment:
                DrawLineSegment(segment, command.Color, command.Options);
                break;
            case Path path:
                DrawPath(path, command.Color, command.Options);
                break;
            case Point point:
                DrawPoint(point, command.Color, command.Options);
                break;
            case Rectangle rectangle:
                DrawRectangle(rectangle, command.Color, command.Options);
                break;
            case Robot robot:
                DrawRobot(robot, command.Color, command.Options);
                break;
            case Text text:
                DrawText(text, command.Color, command.Options);
                break;
            case Triangle triangle:
                DrawTriangle(triangle, command.Color, command.Options);
                break;
        }
    }

    private void DrawArrow(Arrow arrow, Color color, Options options)
    {
        var start = Camera.WorldToScreen(arrow.Start);
        var end = Camera.WorldToScreen(arrow.End);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        // Line part
        _drawList.AddLine(start, end, color.U32, thickness);

        // Arrowhead (simple triangle)
        const float headSize = 20f;
        var headSizeScreen = Camera.WorldToScreenLength(headSize);
        var dir = Vector2.Normalize(end - start);
        var perp = new Vector2(-dir.Y, dir.X); // perpendicular for triangle base

        var tip = end;
        var left = end - dir * headSizeScreen + perp * (headSizeScreen * 0.5f);
        var right = end - dir * headSizeScreen - perp * (headSizeScreen * 0.5f);

        _drawList.AddTriangleFilled(tip, left, right, color.U32);
    }

    private void DrawCircle(Circle circle, Color color, Options options)
    {
        var center = Camera.WorldToScreen(circle.Center);
        var radius = Camera.WorldToScreenLength(circle.Radius);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        if (options.Filled)
            _drawList.AddCircleFilled(center, radius, color.U32, 40);
        else
            _drawList.AddCircle(center, radius, color.U32, 40, thickness);
    }

    private void DrawLine(Line line, Color color, Options options)
    {
        var cameraBounds = Camera.GetVisibleWorldBounds();
        var length = Math.Max(cameraBounds.Width, cameraBounds.Height);

        var dir = line.Angle.ToUnitVec();
        var p1 = line.Point - dir * length;
        var p2 = line.Point + dir * length;

        var start = Camera.WorldToScreen(p1);
        var end = Camera.WorldToScreen(p2);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        _drawList.AddLine(start, end, color.U32, thickness);
    }

    private void DrawLineSegment(LineSegment lineSegment, Color color, Options options)
    {
        var start = Camera.WorldToScreen(lineSegment.Start);
        var end = Camera.WorldToScreen(lineSegment.End);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        _drawList.AddLine(start, end, color.U32, thickness);
    }

    private void DrawPath(Path path, Color color, Options options)
    {
        Span<Vector2> points = stackalloc Vector2[path.Points.Length];
        for (var i = 0; i < points.Length; ++i)
        {
            points[i] = Camera.WorldToScreen(path.Points[i]);
        }

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        unsafe
        {
            fixed (Vector2* ptr = points)
            {
                _drawList.AddPolyline(ptr, points.Length, color.U32, ImDrawFlags.None, thickness);
            }
        }
    }

    private void DrawPoint(Point point, Color color, Options options)
    {
        // draw it as a cross
        const float crossSize = 10f;

        var l1Start = Camera.WorldToScreen(point.Position + new Vector2(-crossSize, -crossSize));
        var l1End = Camera.WorldToScreen(point.Position + new Vector2(crossSize, crossSize));

        var l2Start = Camera.WorldToScreen(point.Position + new Vector2(-crossSize, crossSize));
        var l2End = Camera.WorldToScreen(point.Position + new Vector2(crossSize, -crossSize));

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        _drawList.AddLine(l1Start, l1End, color.U32, thickness);
        _drawList.AddLine(l2Start, l2End, color.U32, thickness);
    }

    private void DrawRectangle(Rectangle rectangle, Color color, Options options)
    {
        var min = Camera.WorldToScreen(rectangle.Min);
        var max = Camera.WorldToScreen(rectangle.Max);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        if (options.Filled)
            _drawList.AddRectFilled(min, max, color.U32);
        else
            _drawList.AddRect(min, max, color.U32, ImDrawFlags.None, thickness);
    }

    private void DrawRobot(Robot robot, Color color, Options options)
    {
        const float robotRadius = 90f;
        const int segments = 20; // circle detail

        var center = Camera.WorldToScreen(robot.Position);
        var radius = Camera.WorldToScreenLength(robotRadius);

        if (robot.Orientation.HasValue)
        {
            var angle = -robot.Orientation.Value.Rad;

            const float flatAngle = MathF.PI / 4f; // amount to "flatten" at the front (adjust as needed)

            var startAngle = angle + flatAngle;
            var endAngle = angle + 2 * MathF.PI - flatAngle;

            Span<Vector2> points = stackalloc Vector2[segments + 1];

            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                var theta = startAngle + (endAngle - startAngle) * t;

                var dir = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
                points[i] = center + dir * radius;
            }

            unsafe
            {
                fixed (Vector2* ptr = points)
                {
                    _drawList.AddConvexPolyFilled(ptr, points.Length, color.U32);
                }
            }
        }
        else
        {
            _drawList.AddCircleFilled(center, radius, color.U32, segments);
        }

        if (robot.Id.HasValue)
        {
            var text = new Text(robot.Id.Value.ToString(), robot.Position, 80f, TextAlignment.Center);
            DrawText(text, Color.Black, new Options());
        }
    }

    private void DrawText(Text text, Color color, Options options)
    {
        var posScreen = Camera.WorldToScreen(text.Position);
        var sizeScreen = Camera.WorldToScreenLength(text.Size);

        if (text.Alignment == TextAlignment.Center)
        {
            var sizeMul = sizeScreen / ImGui.GetFontSize();
            var textSize = ImGui.CalcTextSize(text.Content) * sizeMul;
            posScreen -= textSize * 0.5f;
        }

        unsafe
        {
            _drawList.AddText(ImGui.GetFont().Handle, sizeScreen, posScreen, color.U32, text.Content);
        }
    }

    private void DrawTriangle(Triangle triangle, Color color, Options options)
    {
        var a = Camera.WorldToScreen(triangle.A);
        var b = Camera.WorldToScreen(triangle.B);
        var c = Camera.WorldToScreen(triangle.C);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        if (options.Filled)
            _drawList.AddTriangleFilled(a, b, c, color.U32);
        else
            _drawList.AddTriangle(a, b, c, color.U32, thickness);
    }
}
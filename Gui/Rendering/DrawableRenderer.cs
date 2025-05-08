using System.Numerics;
using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Math;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Color = Tyr.Common.Debug.Drawing.Color;
using Path = Tyr.Common.Debug.Drawing.Drawables.Path;
using Rectangle = Tyr.Common.Debug.Drawing.Drawables.Rectangle;
using Triangle = Tyr.Common.Debug.Drawing.Drawables.Triangle;

namespace Tyr.Gui.Rendering;

[Configurable]
internal partial class DrawableRenderer
{
    [ConfigEntry] private static Color FilledOutlineColor { get; set; } = Color.Zinc950.WithAlpha(0.5f);

    public Camera2D Camera { get; } = new();

    private ImDrawListPtr _drawList;

    internal void Draw(IReadOnlyList<Command> commands, DebugFilter? filter)
    {
        if (commands.Count == 0) return;

        Log.ZLogTrace($"Drawing {commands.Count} items");

        _drawList = ImGui.GetWindowDrawList();

        // restrict the renderings to the camera viewport
        ImGui.PushClipRect(Camera.Viewport.Offset, Camera.Viewport.Offset + Camera.Viewport.Size, true);

        foreach (var command in commands)
        {
            if (filter != null && !filter.IsEnabled(command.Meta)) continue;

            switch (command.Drawable)
            {
                case Arrow arrow:
                    DrawArrow(arrow, command.Color, command.Options);
                    break;
                case Circle circle:
                    DrawCircle(circle, command.Color, command.Options);
                    break;
                case Arc arc:
                    DrawArc(arc, command.Color, command.Options);
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
                    DrawText(text, command.Color);
                    break;
                case Triangle triangle:
                    DrawTriangle(triangle, command.Color, command.Options);
                    break;
            }
        }

        ImGui.PopClipRect();
    }

    private void DrawArrow(Arrow arrow, Color color, Options options)
    {
        Assert.IsFalse(options.IsFilled);

        var start = Camera.WorldToScreen(arrow.Start);
        var end = Camera.WorldToScreen(arrow.End);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        // Line part
        _drawList.AddLine(start, end, ImGui.ColorConvertFloat4ToU32(color), thickness);

        var headSizeScreen = Camera.WorldToScreenLength(arrow.HeadSize);
        var dir = Vector2.Normalize(end - start);
        var perp = new Vector2(-dir.Y, dir.X); // perpendicular for triangle base

        var tip = end;
        var left = end - dir * headSizeScreen + perp * (headSizeScreen * 0.5f);
        var right = end - dir * headSizeScreen - perp * (headSizeScreen * 0.5f);

        _drawList.AddTriangleFilled(tip, left, right, ImGui.ColorConvertFloat4ToU32(color));
    }

    private void DrawCircle(Circle circle, Color color, Options options)
    {
        var center = Camera.WorldToScreen(circle.Center);
        var radius = Camera.WorldToScreenLength(circle.Radius);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        if (options.IsFilled)
        {
            _drawList.AddCircleFilled(center, radius, ImGui.ColorConvertFloat4ToU32(color));
        }

        if (!Utils.ApproximatelyZero(thickness))
        {
            var outlineColor = options.IsFilled ? FilledOutlineColor : color;
            _drawList.AddCircle(center, radius, ImGui.ColorConvertFloat4ToU32(outlineColor), thickness);
        }
    }

    private void DrawArc(Arc arc, Color color, Options options)
    {
        var center = Camera.WorldToScreen(arc.Center);
        var radius = Camera.WorldToScreenLength(arc.Radius);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        if (options.IsFilled)
        {
            _drawList.PathArcTo(center, radius, -arc.Start.Rad, -arc.End.Rad);
            _drawList.PathFillConvex(ImGui.ColorConvertFloat4ToU32(color));
        }

        if (!Utils.ApproximatelyZero(thickness))
        {
            _drawList.PathArcTo(center, radius, -arc.Start.Rad, -arc.End.Rad);
            var outlineColor = options.IsFilled ? FilledOutlineColor : color;
            var flags = arc.Closed ? ImDrawFlags.Closed : ImDrawFlags.None;
            _drawList.PathStroke(ImGui.ColorConvertFloat4ToU32(outlineColor), flags, thickness);
        }
    }

    private void DrawLine(Line line, Color color, Options options)
    {
        Assert.IsFalse(options.IsFilled);

        var cameraBounds = Camera.GetVisibleWorldBounds();
        var length = Math.Max(cameraBounds.Width, cameraBounds.Height);

        var dir = line.Angle.ToUnitVec();
        var p1 = line.Point - dir * length;
        var p2 = line.Point + dir * length;

        var start = Camera.WorldToScreen(p1);
        var end = Camera.WorldToScreen(p2);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        _drawList.AddLine(start, end, ImGui.ColorConvertFloat4ToU32(color), thickness);
    }

    private void DrawLineSegment(LineSegment lineSegment, Color color, Options options)
    {
        Assert.IsFalse(options.IsFilled);

        var start = Camera.WorldToScreen(lineSegment.Start);
        var end = Camera.WorldToScreen(lineSegment.End);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        _drawList.AddLine(start, end, ImGui.ColorConvertFloat4ToU32(color), thickness);
    }

    private void DrawPath(Path path, Color color, Options options)
    {
        Assert.IsFalse(options.IsFilled);

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
                _drawList.AddPolyline(ptr, points.Length, ImGui.ColorConvertFloat4ToU32(color), ImDrawFlags.None,
                    thickness);
            }
        }
    }

    private void DrawPoint(Point point, Color color, Options options)
    {
        Assert.IsFalse(options.IsFilled);

        var l1Start = Camera.WorldToScreen(point.Position + new Vector2(-point.Size, -point.Size));
        var l1End = Camera.WorldToScreen(point.Position + new Vector2(point.Size, point.Size));

        var l2Start = Camera.WorldToScreen(point.Position + new Vector2(-point.Size, point.Size));
        var l2End = Camera.WorldToScreen(point.Position + new Vector2(point.Size, -point.Size));

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        _drawList.AddLine(l1Start, l1End, ImGui.ColorConvertFloat4ToU32(color), thickness);
        _drawList.AddLine(l2Start, l2End, ImGui.ColorConvertFloat4ToU32(color), thickness);
    }

    private void DrawRectangle(Rectangle rectangle, Color color, Options options)
    {
        var min = Camera.WorldToScreen(rectangle.Min);
        var max = Camera.WorldToScreen(rectangle.Max);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        if (options.IsFilled)
        {
            _drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(color));
        }

        if (!Utils.ApproximatelyZero(thickness))
        {
            var outlineColor = options.IsFilled ? FilledOutlineColor : color;
            _drawList.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(outlineColor), ImDrawFlags.None, thickness);
        }
    }

    private void DrawRobot(Robot robot, Color color, Options options)
    {
        if (robot.Orientation.HasValue)
        {
            var arc = new Arc(robot.Position, robot.Radius,
                robot.Orientation.Value + Robot.FlatAngle,
                robot.Orientation.Value + 2f * Angle.Pi - Robot.FlatAngle,
                true);
            DrawArc(arc, color, options);
        }
        else
        {
            var circle = new Circle(robot.Position, robot.Radius);
            DrawCircle(circle, color, options);
        }

        if (robot.Id.HasValue)
        {
            var text = new Text(robot.Id.Value.ToString(), robot.Position, Robot.TextSize, TextAlignment.Center);
            DrawText(text, Robot.TextColor);
        }
    }

    private void DrawText(Text text, Color color)
    {
        var posScreen = Camera.WorldToScreen(text.Position);
        var sizeScreen = Camera.WorldToScreenLength(text.Size);

        var (font, correctedSize) = FontRegistry.Instance.GetFieldFont(sizeScreen);

        var textSize = ImGui.CalcTextSizeA(font, correctedSize,
            float.PositiveInfinity, float.PositiveInfinity,
            text.Content);

        if ((text.Alignment & TextAlignment.HCenter) != 0)
        {
            posScreen.X -= textSize.X * 0.5f;
        }
        else if ((text.Alignment & TextAlignment.Right) != 0)
        {
            posScreen.X -= textSize.X;
        }

        if ((text.Alignment & TextAlignment.VCenter) != 0)
        {
            posScreen.Y -= textSize.Y * 0.5f;
        }
        else if ((text.Alignment & TextAlignment.Bottom) != 0)
        {
            posScreen.Y -= textSize.Y;
        }

        unsafe
        {
            _drawList.AddText(font.Handle, correctedSize, posScreen,
                ImGui.ColorConvertFloat4ToU32(color), text.Content);
        }
    }

    private void DrawTriangle(Triangle triangle, Color color, Options options)
    {
        var a = Camera.WorldToScreen(triangle.A);
        var b = Camera.WorldToScreen(triangle.B);
        var c = Camera.WorldToScreen(triangle.C);

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        if (options.IsFilled)
        {
            _drawList.AddTriangleFilled(a, b, c, ImGui.ColorConvertFloat4ToU32(color));
        }

        if (!Utils.ApproximatelyZero(thickness))
        {
            var outlineColor = options.IsFilled ? FilledOutlineColor : color;
            _drawList.AddTriangle(a, b, c, ImGui.ColorConvertFloat4ToU32(outlineColor), thickness);
        }
    }
}
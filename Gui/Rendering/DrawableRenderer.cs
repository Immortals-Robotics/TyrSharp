using System.Numerics;
using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Math;
using Tyr.Gui.Backend;
using Tyr.Soccer.Navigation.Trajectory;
using Color = Tyr.Common.Debug.Drawing.Color;
using Path = Tyr.Common.Debug.Drawing.Drawables.Path;
using Point = Tyr.Common.Debug.Drawing.Drawables.Point;
using Rectangle = Tyr.Common.Debug.Drawing.Drawables.Rectangle;
using Triangle = Tyr.Common.Debug.Drawing.Drawables.Triangle;

namespace Tyr.Gui.Rendering;

[Configurable]
internal partial class DrawableRenderer
{
    [ConfigEntry] private static Color FilledOutlineColor { get; set; } = Color.Zinc950.WithAlpha(0.5f);
    [ConfigEntry] private static float TrajectoryDrawTimeStep { get; set; } = 0.1f;

    public Camera2D Camera { get; } = new();

    private ImDrawListPtr _drawList;

    internal void Draw(IReadOnlyList<Rectangle> rectangles, bool pushClipRect = true)
    {
        if (!BeginDraw(rectangles.Count, pushClipRect)) return;
        foreach (var rectangle in rectangles) DrawRectangle(rectangle);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<Circle> circles, bool pushClipRect = true)
    {
        if (!BeginDraw(circles.Count, pushClipRect)) return;
        foreach (var circle in circles) DrawCircle(circle);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<Arc> arcs, bool pushClipRect = true)
    {
        if (!BeginDraw(arcs.Count, pushClipRect)) return;
        foreach (var arc in arcs) DrawArc(arc);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<Arrow> arrows, bool pushClipRect = true)
    {
        if (!BeginDraw(arrows.Count, pushClipRect)) return;
        foreach (var arrow in arrows) DrawArrow(arrow);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<Line> lines, bool pushClipRect = true)
    {
        if (!BeginDraw(lines.Count, pushClipRect)) return;
        foreach (var line in lines) DrawLine(line);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<LineSegment> lineSegments, bool pushClipRect = true)
    {
        if (!BeginDraw(lineSegments.Count, pushClipRect)) return;
        foreach (var lineSegment in lineSegments) DrawLineSegment(lineSegment);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<Path> paths, bool pushClipRect = true)
    {
        if (!BeginDraw(paths.Count, pushClipRect)) return;
        foreach (var path in paths) DrawPath(path);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<Point> points, bool pushClipRect = true)
    {
        if (!BeginDraw(points.Count, pushClipRect)) return;
        foreach (var point in points) DrawPoint(point);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<Triangle> triangles, bool pushClipRect = true)
    {
        if (!BeginDraw(triangles.Count, pushClipRect)) return;
        foreach (var triangle in triangles) DrawTriangle(triangle);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<Robot> robots, bool pushClipRect = true)
    {
        if (!BeginDraw(robots.Count, pushClipRect)) return;
        foreach (var robot in robots) DrawRobot(robot);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<Text> texts, bool pushClipRect = true)
    {
        if (!BeginDraw(texts.Count, pushClipRect)) return;
        foreach (var text in texts) DrawText(text);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<Trajectory2DDrawable> trajectories, bool pushClipRect = true)
    {
        if (!BeginDraw(trajectories.Count, pushClipRect)) return;
        foreach (var trajectory in trajectories) DrawTrajectory(trajectory);
        EndDraw(pushClipRect);
    }

    internal void Draw(IReadOnlyList<Trajectory2DChainedDrawable> trajectories, bool pushClipRect = true)
    {
        if (!BeginDraw(trajectories.Count, pushClipRect)) return;
        foreach (var trajectory in trajectories) DrawTrajectory(trajectory);
        EndDraw(pushClipRect);
    }

    private bool BeginDraw(int count, bool pushClipRect)
    {
        if (count == 0)
            return false;

        _drawList = ImGui.GetWindowDrawList();
        if (pushClipRect)
            ImGui.PushClipRect(Camera.Viewport.Offset, Camera.Viewport.Offset + Camera.Viewport.Size, true);

        return true;
    }

    private static void EndDraw(bool pushClipRect)
    {
        if (pushClipRect)
            ImGui.PopClipRect();
    }

    private void DrawArrow(Arrow arrow)
    {
        Assert.IsFalse(arrow.Options.IsFilled);

        var start = Camera.WorldToScreen(arrow.Start);
        var end = Camera.WorldToScreen(arrow.End);

        var thickness = Camera.WorldToScreenLength(arrow.Options.Thickness);

        _drawList.AddLine(start, end, ImGui.ColorConvertFloat4ToU32(arrow.Color), thickness);

        var headSizeScreen = Camera.WorldToScreenLength(arrow.HeadSize);
        var dir = Vector2.Normalize(end - start);
        var perp = new Vector2(-dir.Y, dir.X);

        var tip = end;
        var left = end - dir * headSizeScreen + perp * (headSizeScreen * 0.5f);
        var right = end - dir * headSizeScreen - perp * (headSizeScreen * 0.5f);

        _drawList.AddTriangleFilled(tip, left, right, ImGui.ColorConvertFloat4ToU32(arrow.Color));
    }

    private void DrawCircle(Circle circle)
    {
        var center = Camera.WorldToScreen(circle.Center);
        var radius = Camera.WorldToScreenLength(circle.Radius);

        var thickness = Camera.WorldToScreenLength(circle.Options.Thickness);

        if (circle.Options.IsFilled)
            _drawList.AddCircleFilled(center, radius, ImGui.ColorConvertFloat4ToU32(circle.Color), 64);

        if (!Utils.ApproximatelyZero(thickness))
        {
            var outlineColor = circle.Options.IsFilled ? FilledOutlineColor : circle.Color;
            _drawList.AddCircle(center, radius, ImGui.ColorConvertFloat4ToU32(outlineColor), 64, thickness);
        }
    }

    private void DrawArc(Arc arc)
    {
        var center = Camera.WorldToScreen(arc.Center);
        var radius = Camera.WorldToScreenLength(arc.Radius);

        var thickness = Camera.WorldToScreenLength(arc.Options.Thickness);

        if (arc.Options.IsFilled)
        {
            _drawList.PathArcTo(center, radius, -arc.Start.Rad, -arc.End.Rad, 64);
            _drawList.PathFillConvex(ImGui.ColorConvertFloat4ToU32(arc.Color));
        }

        if (!Utils.ApproximatelyZero(thickness))
        {
            _drawList.PathArcTo(center, radius, -arc.Start.Rad, -arc.End.Rad, 64);
            var outlineColor = arc.Options.IsFilled ? FilledOutlineColor : arc.Color;
            var flags = arc.Closed ? ImDrawFlags.Closed : ImDrawFlags.None;
            _drawList.PathStroke(ImGui.ColorConvertFloat4ToU32(outlineColor), flags, thickness);
        }
    }

    private void DrawLine(Line line)
    {
        Assert.IsFalse(line.Options.IsFilled);

        var cameraBounds = Camera.GetVisibleWorldBounds();
        var length = Math.Max(cameraBounds.Width, cameraBounds.Height);

        var dir = line.Angle.ToUnitVec();
        var p1 = line.Point - dir * length;
        var p2 = line.Point + dir * length;

        var start = Camera.WorldToScreen(p1);
        var end = Camera.WorldToScreen(p2);

        var thickness = Camera.WorldToScreenLength(line.Options.Thickness);

        _drawList.AddLine(start, end, ImGui.ColorConvertFloat4ToU32(line.Color), thickness);
    }

    private void DrawLineSegment(LineSegment lineSegment)
    {
        Assert.IsFalse(lineSegment.Options.IsFilled);

        var start = Camera.WorldToScreen(lineSegment.Start);
        var end = Camera.WorldToScreen(lineSegment.End);

        var thickness = Camera.WorldToScreenLength(lineSegment.Options.Thickness);

        _drawList.AddLine(start, end, ImGui.ColorConvertFloat4ToU32(lineSegment.Color), thickness);
    }

    private void DrawPath(Path path)
    {
        Assert.IsFalse(path.Options.IsFilled);

        Span<Vector2> points = stackalloc Vector2[path.Points.Length];
        for (var i = 0; i < points.Length; ++i)
            points[i] = Camera.WorldToScreen(path.Points[i]);

        var thickness = Camera.WorldToScreenLength(path.Options.Thickness);

        unsafe
        {
            fixed (Vector2* ptr = points)
            {
                _drawList.AddPolyline(ptr, points.Length, ImGui.ColorConvertFloat4ToU32(path.Color), ImDrawFlags.None, thickness);
            }
        }
    }

    private void DrawPoint(Point point)
    {
        Assert.IsFalse(point.Options.IsFilled);

        var l1Start = Camera.WorldToScreen(point.Position + new Vector2(-point.Size, -point.Size));
        var l1End = Camera.WorldToScreen(point.Position + new Vector2(point.Size, point.Size));

        var l2Start = Camera.WorldToScreen(point.Position + new Vector2(-point.Size, point.Size));
        var l2End = Camera.WorldToScreen(point.Position + new Vector2(point.Size, -point.Size));

        var thickness = Camera.WorldToScreenLength(point.Options.Thickness);

        _drawList.AddLine(l1Start, l1End, ImGui.ColorConvertFloat4ToU32(point.Color), thickness);
        _drawList.AddLine(l2Start, l2End, ImGui.ColorConvertFloat4ToU32(point.Color), thickness);
    }

    private void DrawRectangle(Rectangle rectangle)
    {
        var min = Camera.WorldToScreen(rectangle.Min);
        var max = Camera.WorldToScreen(rectangle.Max);

        var thickness = Camera.WorldToScreenLength(rectangle.Options.Thickness);

        if (rectangle.Options.IsFilled)
            _drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(rectangle.Color));

        if (!Utils.ApproximatelyZero(thickness))
        {
            var outlineColor = rectangle.Options.IsFilled ? FilledOutlineColor : rectangle.Color;
            _drawList.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(outlineColor), ImDrawFlags.None, thickness);
        }
    }

    private void DrawRobot(Robot robot)
    {
        if (robot.Orientation.HasValue)
        {
            var arc = new Arc
            {
                Center = robot.Position,
                Radius = robot.Radius,
                Start = robot.Orientation.Value + Robot.FlatAngle,
                End = robot.Orientation.Value + 2f * Angle.Pi - Robot.FlatAngle,
                Closed = true,
                Color = robot.Color,
                Options = robot.Options,
            };
            DrawArc(arc);
        }
        else
        {
            var circle = new Circle { Center = robot.Position, Radius = robot.Radius, Color = robot.Color, Options = robot.Options };
            DrawCircle(circle);
        }

        if (robot.Id.HasValue)
        {
            var text = new Text
            {
                Content = robot.Id.Value.ToString(),
                Position = robot.Position,
                Size = Robot.TextSize,
                Alignment = TextAlignment.Center,
                Color = Robot.TextColor,
            };
            DrawText(text);
        }
    }

    private void DrawText(Text text)
    {
        if (string.IsNullOrEmpty(text.Content))
            return;

        var posScreen = Camera.WorldToScreen(text.Position);
        var sizeScreen = Camera.WorldToScreenLength(text.Size);

        var (font, correctedSize) = FontRegistry.Instance.GetFieldFont(sizeScreen);
        if (font.IsNull || !float.IsFinite(correctedSize) || correctedSize <= 0f)
            return;

        var textSize = ImGui.CalcTextSizeA(font, correctedSize,
            float.PositiveInfinity, float.PositiveInfinity,
            text.Content);

        if ((text.Alignment & TextAlignment.HCenter) != 0)
            posScreen.X -= textSize.X * 0.5f;
        else if ((text.Alignment & TextAlignment.Right) != 0)
            posScreen.X -= textSize.X;

        if ((text.Alignment & TextAlignment.VCenter) != 0)
            posScreen.Y -= textSize.Y * 0.5f;
        else if ((text.Alignment & TextAlignment.Bottom) != 0)
            posScreen.Y -= textSize.Y;

        unsafe
        {
            _drawList.AddText(font.Handle, correctedSize, posScreen,
                ImGui.ColorConvertFloat4ToU32(text.Color), text.Content);
        }
    }

    private void DrawTriangle(Triangle triangle)
    {
        var a = Camera.WorldToScreen(triangle.A);
        var b = Camera.WorldToScreen(triangle.B);
        var c = Camera.WorldToScreen(triangle.C);

        var thickness = Camera.WorldToScreenLength(triangle.Options.Thickness);

        if (triangle.Options.IsFilled)
            _drawList.AddTriangleFilled(a, b, c, ImGui.ColorConvertFloat4ToU32(triangle.Color));

        if (!Utils.ApproximatelyZero(thickness))
        {
            var outlineColor = triangle.Options.IsFilled ? FilledOutlineColor : triangle.Color;
            _drawList.AddTriangle(a, b, c, ImGui.ColorConvertFloat4ToU32(outlineColor), thickness);
        }
    }

    private void DrawTrajectory(Trajectory2DDrawable trajectory)
    {
        DrawTrajectoryCore(trajectory.Trajectory.StartTime, trajectory.Trajectory.EndTime,
            trajectory.Trajectory.GetPosition, trajectory.Color, trajectory.Options);
    }

    private void DrawTrajectory(Trajectory2DChainedDrawable trajectory)
    {
        DrawTrajectoryCore(trajectory.Trajectory.StartTime, trajectory.Trajectory.EndTime,
            trajectory.Trajectory.GetPosition, trajectory.Color, trajectory.Options);
    }

    private void DrawTrajectoryCore(float startTime, float endTime, Func<float, Vector2> getPosition, Color color, Options options)
    {
        Assert.IsFalse(options.IsFilled);

        if (TrajectoryDrawTimeStep <= 0f)
            throw new ArgumentOutOfRangeException(nameof(TrajectoryDrawTimeStep), TrajectoryDrawTimeStep,
                "Time step must be positive.");

        if (endTime < startTime)
            return;

        var sampleCount = 1;
        for (var t = startTime; t < endTime; t += TrajectoryDrawTimeStep)
            sampleCount++;

        if (sampleCount < 2)
            return;

        Span<Vector2> screenPoints = stackalloc Vector2[sampleCount];
        var index = 0;
        for (var t = startTime; t < endTime; t += TrajectoryDrawTimeStep)
            screenPoints[index++] = Camera.WorldToScreen(getPosition(t));

        screenPoints[index] = Camera.WorldToScreen(getPosition(endTime));

        var thickness = Camera.WorldToScreenLength(options.Thickness);

        unsafe
        {
            fixed (Vector2* ptr = screenPoints)
            {
                _drawList.AddPolyline(ptr, index + 1, ImGui.ColorConvertFloat4ToU32(color),
                    ImDrawFlags.None, thickness);
            }
        }
    }
}

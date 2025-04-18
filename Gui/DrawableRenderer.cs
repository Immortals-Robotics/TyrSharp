using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.Mathematics;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Path = Tyr.Common.Debug.Drawing.Drawables.Path;
using Rectangle = Tyr.Common.Debug.Drawing.Drawables.Rectangle;
using Triangle = Tyr.Common.Debug.Drawing.Drawables.Triangle;

namespace Tyr.Gui;

internal class DrawableRenderer
{
    public Camera2D Camera { get; set; } = new();

    internal void Draw(Command command)
    {
        var drawList = ImGui.GetWindowDrawList();

        var thickness = Camera.WorldToScreenLength(command.Options.Thickness);
        var col32 = Colors.Red.ToUIntRGBA();

        switch (command.Drawable)
        {
            case Arrow arrow:
                throw new NotImplementedException();
            case Circle circle:
            {
                var center = Camera.WorldToScreen(circle.Center);
                var radius = Camera.WorldToScreenLength(circle.Radius);

                if (command.Options.Filled)
                    drawList.AddCircleFilled(center, radius, col32, 40);
                else
                    drawList.AddCircle(center, radius, col32, 40, thickness);
                break;
            }
            case Line line:
                throw new NotImplementedException();
            case LineSegment segment:
            {
                var start = Camera.WorldToScreen(segment.Start);
                var end = Camera.WorldToScreen(segment.End);

                drawList.AddLine(start, end, col32, thickness);

                break;
            }

            case Path path:
            {
                var points = new Vector2[path.Points.Length];
                for (var i = 0; i < points.Length; ++i)
                {
                    points[i] = Camera.WorldToScreen(path.Points[i]);
                }

                unsafe
                {
                    fixed (Vector2* ptr = points)
                    {
                        drawList.AddPolyline(ptr, points.Length, col32, ImDrawFlags.None, thickness);
                    }
                }

                break;
            }

            case Point point:
            {
                // draw it as a cross
                const float crossSize = 10f;

                var l1Start = Camera.WorldToScreen(point.Position + new Vector2(-crossSize, -crossSize));
                var l1End = Camera.WorldToScreen(point.Position + new Vector2(crossSize, crossSize));

                var l2Start = Camera.WorldToScreen(point.Position + new Vector2(-crossSize, crossSize));
                var l2End = Camera.WorldToScreen(point.Position + new Vector2(crossSize, -crossSize));

                drawList.AddLine(l1Start, l1End, col32, thickness);
                drawList.AddLine(l2Start, l2End, col32, thickness);

                break;
            }

            case Rectangle rectangle:
            {
                var min = Camera.WorldToScreen(rectangle.Min);
                var max = Camera.WorldToScreen(rectangle.Max);

                if (command.Options.Filled)
                    drawList.AddRectFilled(min, max, col32);
                else
                    drawList.AddRect(min, max, col32, ImDrawFlags.None, thickness);

                break;
            }

            case Robot robot:
                throw new NotImplementedException();

            case Text text:
                throw new NotImplementedException();

            case Triangle triangle:
                throw new NotImplementedException();
        }
    }
}
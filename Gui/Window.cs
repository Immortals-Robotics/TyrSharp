using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Math;
using Color = Tyr.Common.Debug.Drawing.Color;
using Path = Tyr.Common.Debug.Drawing.Drawables.Path;
using Triangle = Tyr.Common.Debug.Drawing.Drawables.Triangle;

namespace Tyr.Gui;

public class Window : ImWindow
{
    public override string Name => "Window";

    private readonly DrawableRenderer _renderer = new();

    public override void DrawContent()
    {
        ImGui.Begin("Hippos");

        _renderer.Camera.Position = Vector2.Zero;
        _renderer.Camera.Zoom = 0.3f;
        _renderer.Camera.Viewport = new Viewport(
            Offset: ImGui.GetCursorScreenPos(),
            Size: ImGui.GetContentRegionAvail());

        var commands = new List<Command>();

        var circleDrawable = new Circle(new Vector2(0f, 400f), 50f);
        var pointDrawable = new Point(new Vector2(0f, 0f));
        var arrowDrawable = new Arrow(Vector2.Zero, new Vector2(100f, 100f));
        var lineDrawable = new Line(new Vector2(-100f, -100f), Angle.FromDeg(45f));
        var textDrawable = new Text("Have no fear,\nHippo is here", new Vector2(100f, 100f), 50f);
        var triangleDrawable = new Triangle(Vector2.Zero, new Vector2(50f, 100f), new Vector2(100f, 0f));
        var robotDrawable = new Robot(new Vector2(200f, 200f), Angle.FromDeg(0f), 8);

        var spiralPath = new Vector2[40];

        for (var i = 0; i < spiralPath.Length; i++)
        {
            var t = i / 40f; // [0, 1]  
            var angle = t * MathF.PI * 4; // 2 full turns
            var radius = t * 1000f; // up to 1000 units

            var x = MathF.Cos(angle) * radius;
            var y = MathF.Sin(angle) * radius;

            spiralPath[i] = new Vector2(x, y);
        }

        var pathDrawable = new Path(spiralPath);

        var options = new Options(Filled: false, Thickness: 5f);
        var meta = new Meta("Gui", DateTime.UtcNow, 0, null, null, 0);

        commands.Add(new Command(circleDrawable, Color.Yellow, options, meta));
        commands.Add(new Command(pointDrawable, Color.Yellow, options, meta));
        commands.Add(new Command(arrowDrawable, Color.Yellow, options, meta));
        commands.Add(new Command(lineDrawable, Color.Yellow, options, meta));
        commands.Add(new Command(textDrawable, Color.Yellow, options, meta));
        commands.Add(new Command(triangleDrawable, Color.Yellow, options, meta));
        commands.Add(new Command(robotDrawable, Color.Yellow, options, meta));
        commands.Add(new Command(pathDrawable, Color.Yellow, options, meta));

        _renderer.Draw(commands);

        ImGui.End();
    }
}
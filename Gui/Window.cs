using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Math;
using Color = Tyr.Common.Debug.Drawing.Color;
using Path = Tyr.Common.Debug.Drawing.Drawables.Path;
using Random = Tyr.Common.Math.Random;
using Triangle = Tyr.Common.Debug.Drawing.Drawables.Triangle;

namespace Tyr.Gui;

public class Window : ImWindow
{
    public override string Name => "Window";

    private readonly DrawableRenderer _renderer = new();

    private readonly Common.Time.Timer _timer = new();
    private readonly List<Command> _commands = [];

    public Window()
    {
        for (var i = 0; i < 1000; i++)
        {
            var circleDrawable = new Circle(new Vector2(Random.Get(-500f, 500f), Random.Get(-500f, 500f)),
                Random.Get(10f, 100f));
            var pointDrawable = new Point(new Vector2(Random.Get(-500f, 500f), Random.Get(-500f, 500f)));
            var arrowDrawable = new Arrow(new Vector2(Random.Get(-500f, 500f), Random.Get(-500f, 500f)),
                new Vector2(Random.Get(-500f, 500f), Random.Get(-500f, 500f)));
            var lineDrawable = new Line(new Vector2(Random.Get(-500f, 500f), Random.Get(-500f, 500f)),
                Angle.FromDeg(Random.Get(0f, 360f)));
            var textDrawable = new Text("Have no fear,\nHippo is here",
                new Vector2(Random.Get(-500f, 500f), Random.Get(-500f, 500f)), Random.Get(20f, 80f));
            var triangleDrawable = new Triangle(
                new Vector2(Random.Get(-500f, 500f), Random.Get(-500f, 500f)),
                new Vector2(Random.Get(-500f, 500f), Random.Get(-500f, 500f)),
                new Vector2(Random.Get(-500f, 500f), Random.Get(-500f, 500f)));
            var robotDrawable = new Robot(new Vector2(Random.Get(-500f, 500f), Random.Get(-500f, 500f)),
                Angle.FromDeg(Random.Get(0f, 360f)), i % 20);

            var spiralPath = new Vector2[40];
            for (var pathIdx = 0; pathIdx < spiralPath.Length; pathIdx++)
            {
                var t = pathIdx / 40f; // [0, 1]  
                var angle = t * MathF.PI * 4; // 2 full turns
                var radius = t * 1000f; // up to 1000 units

                var x = MathF.Cos(angle) * radius;
                var y = MathF.Sin(angle) * radius;

                spiralPath[pathIdx] = new Vector2(x, y);
            }

            var pathDrawable = new Path(spiralPath);

            var options = new Options(Filled: Random.Get(0f, 1f) > 0.5f, Thickness: Random.Get(1f, 10f));
            var meta = new Meta("Gui", DateTime.UtcNow, 0, null, null, 0);

            _commands.Add(new Command(circleDrawable, Color.Random(), options, meta));
            _commands.Add(new Command(pointDrawable, Color.Random(), options, meta));
            _commands.Add(new Command(arrowDrawable, Color.Random(), options, meta));
            _commands.Add(new Command(lineDrawable, Color.Random(), options, meta));
            _commands.Add(new Command(textDrawable, Color.Random(), options, meta));
            _commands.Add(new Command(triangleDrawable, Color.Random(), options, meta));
            _commands.Add(new Command(robotDrawable, Color.Random(), options, meta));
            _commands.Add(new Command(pathDrawable, Color.Random(), options, meta));
        }
    }

    public override void Init()
    {
        base.Init();
        _timer.Start();
    }

    public override void DrawContent()
    {
        ImGui.Begin("Hippos");

        _renderer.Camera.Position = Vector2.Zero;
        _renderer.Camera.Zoom = 0.3f;
        _renderer.Camera.Viewport = new Viewport(
            Offset: ImGui.GetCursorScreenPos(),
            Size: ImGui.GetContentRegionAvail());

        _renderer.Draw(_commands);

        _timer.Update();
        Logger.ZLogTrace($"FPS: {_timer.FpsSmooth}");

        ImGui.End();
    }
}
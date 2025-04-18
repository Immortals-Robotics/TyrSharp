using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets;
using Hexa.NET.Mathematics;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Math;
using Color = Tyr.Common.Debug.Drawing.Color;
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

        //var drawable = new Circle(new Vector2(0f, 400f), 50f);
        //var drawable = new Point(new Vector2(0f, 0f));
        //var drawable = new Arrow(Vector2.Zero, new Vector2(100f, 100f));
        //var drawable = new Line(new Vector2(-100f, -100f), Angle.FromDeg(45f));
        //var drawable = new Text("Have no fear,\nHippo is here", new Vector2(100f, 100f), 50f);
        //var drawable = new Triangle(Vector2.Zero, new Vector2(50f, 100f), new Vector2(100f, 0f));
        var drawable = new Robot(new Vector2(200f, 200f), Angle.FromDeg(0f), 8);
        var options = new Options(Filled: false, Thickness: 5f);
        var command = new Command(drawable, Color.Yellow, options,
            "Gui", DateTime.UtcNow, "", "", 0);

        _renderer.Draw(command);

        ImGui.End();
    }
}
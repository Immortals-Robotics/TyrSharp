using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets;
using Hexa.NET.Mathematics;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui;

public class Window : ImWindow
{
    public override string Name => "Window";

    private DrawableRenderer _renderer = new();

    public override void DrawContent()
    {
        ImGui.Begin("Hippos");

        _renderer.Camera.Position = Vector2.Zero;
        _renderer.Camera.Zoom = 0.5f;
        _renderer.Camera.Viewport = new Viewport(
            Offset: ImGui.GetCursorScreenPos(),
            Size: ImGui.GetContentRegionAvail());

        //var drawable = new Circle(new Vector2(0f, 400f), 50f);
        var drawable = new Point(new Vector2(0f, 0f));
        var options = new Options(Filled: false, Thickness: 5f);
        var command = new Command(drawable, Color.Black, options,
            "Gui", DateTime.UtcNow, "", "", 0);

        _renderer.Draw(command);

        ImGui.End();
    }
}
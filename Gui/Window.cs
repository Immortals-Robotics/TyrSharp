using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets;

namespace Tyr.Gui;

public class Window : ImWindow
{
    public override string Name => "Window";

    private readonly DrawableRenderer _renderer = new();

    private readonly Common.Time.Timer _timer = new();

    private readonly DebugFramer _framer = new();

    public override void Init()
    {
        base.Init();
        _timer.Start();
    }

    public override void DrawContent()
    {
        _framer.Tick();

        ImGui.Begin("Hippos");

        _renderer.Camera.Position = Vector2.Zero;
        _renderer.Camera.Zoom = 0.1f;
        _renderer.Camera.Viewport = new Viewport(
            Offset: ImGui.GetCursorScreenPos(),
            Size: ImGui.GetContentRegionAvail());

        foreach (var module in _framer.Modules.Values)
        {
            var frame = module.LatestDisplayableFrame;
            if (frame == null) continue;

            _renderer.Draw(frame.Draws);
        }

        _timer.Update();
        Logger.ZLogTrace($"FPS: {_timer.FpsSmooth}");

        ImGui.End();
    }
}
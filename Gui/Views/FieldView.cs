using System.Numerics;
using Hexa.NET.ImGui;
using Tyr.Common.Debug;
using Tyr.Common.Math;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Tyr.Gui.Rendering;

namespace Tyr.Gui.Views;

public class FieldView
{
    private readonly DebugFramer _debugFramer;
    private readonly DebugFilter _filter;
    private readonly DrawableRenderer _renderer = new();
    private readonly Common.Time.Timer _timer = new();

    public FieldView(DebugFramer debugFramer, DebugFilter filter)
    {
        _debugFramer = debugFramer;
        _filter = filter;
        ModuleContext.Current.Value = ModuleName;

        _timer.Start();

        _renderer.Camera.Position = Vector2.Zero;
        _renderer.Camera.Zoom = 0.1f;
    }

    public void Draw(PlaybackTime time)
    {
        _timer.Update();

        if (ImGui.Begin($"{IconFonts.FontAwesome6.Video} Field"))
        {
            ImGui.PushFont(FontRegistry.Instance.MonoFont);

            _renderer.Camera.Viewport = new Viewport(
                Offset: ImGui.GetCursorScreenPos(),
                Size: ImGui.GetContentRegionAvail());

            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.None))
            {
                // zooming
                if (!Utils.ApproximatelyZero(ImGui.GetIO().MouseWheel))
                {
                    var zoomFactor = 1.1f; // Adjust as needed for smoother/quicker zooming
                    var newZoom = ImGui.GetIO().MouseWheel > 0
                        ? _renderer.Camera.Zoom * zoomFactor
                        : _renderer.Camera.Zoom / zoomFactor;

                    var mouseScreen = ImGui.GetMousePos();
                    var mouseWorldBefore = _renderer.Camera.ScreenToWorld(mouseScreen);

                    _renderer.Camera.Zoom = newZoom;

                    var mouseWorldAfter = _renderer.Camera.ScreenToWorld(mouseScreen);
                    _renderer.Camera.Position -= mouseWorldAfter - mouseWorldBefore;
                }

                // panning
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var mouseDelta = ImGui.GetIO().MouseDelta;
                    _renderer.Camera.Position -= _renderer.Camera.ScreenToWorldDirection(mouseDelta);
                }
            }

            foreach (var (module, framer) in _debugFramer.Modules)
            {
                if (!_filter.IsEnabled(module)) continue;

                var frame = time.Live ? framer.LatestFrame : framer.GetFrame(time.Time);
                if (frame == null) continue;

                _renderer.Draw(frame.Draws, _filter);
            }

            ShowStats();

            ImGui.PopFont();
        }

        ImGui.End();
    }

    private void ShowStats()
    {
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav |
                    ImGuiWindowFlags.NoMove;

        var padding = ImGui.GetStyle().WindowPadding;
        ImGui.SetNextWindowPos(_renderer.Camera.Viewport.Offset + padding, ImGuiCond.Always, Vector2.Zero);
        ImGui.SetNextWindowBgAlpha(0.35f);

        if (ImGui.Begin("Stats", flags))
        {
            ImGui.Text($"FPS: {_timer.FpsSmooth:F0}");
        }

        ImGui.End();
    }
}
using System.Numerics;
using Hexa.NET.ImGui;
using Tyr.Common.Debug;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Gui.Data;
using Tyr.Gui.Rendering;

namespace Tyr.Gui.Views;

public class FieldView
{
    private readonly DrawableRenderer _renderer = new();

    private readonly Common.Time.Timer _timer = new();

    private readonly DebugFramer _framer = new();

    private readonly DebugFilter _filter = new();

    private float _time;
    private bool _live = true;

    public FieldView()
    {
        ModuleContext.Current.Value = ModuleName;

        _timer.Start();

        _renderer.Camera.Position = Vector2.Zero;
        _renderer.Camera.Zoom = 0.1f;
    }

    public void Draw()
    {
        ImGui.Begin("Field");
        ImGui.PushFont(FontRegistry.Instance.MonoFont);

        _timer.Update();
        _framer.Tick();

        ImGui.Text($"FPS: {_timer.FpsSmooth:F1}");

        foreach (var module in _framer.Modules.Keys)
        {
            _filter.Register(module);
        }

        _filter.Draw();

        var start = Timestamp.MaxValue;
        var end = Timestamp.Zero;

        foreach (var framer in _framer.Modules.Values)
        {
            if (!framer.StartTime.HasValue || !framer.EndTime.HasValue) continue;

            start = Timestamp.Min(start, framer.StartTime.Value);
            end = Timestamp.Max(end, framer.EndTime.Value);
        }

        start = Timestamp.Clamp(start, Timestamp.Zero, end);

        var endDelta = (float)(end - start).Seconds;
        if (_live) _time = endDelta;

        ImGui.SliderFloat("Time", ref _time, 0f, endDelta);
        ImGui.SameLine();
        ImGui.Checkbox("Live", ref _live);

        _renderer.Camera.Viewport = new Viewport(
            Offset: ImGui.GetCursorScreenPos(),
            Size: ImGui.GetContentRegionAvail());

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
        if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
        {
            var mouseDelta = ImGui.GetIO().MouseDelta;
            _renderer.Camera.Position -= _renderer.Camera.ScreenToWorldDirection(mouseDelta);
        }

        foreach (var (module, framer) in _framer.Modules)
        {
            if (!_filter.IsEnabled(module)) continue;

            var frame = _live ? framer.LatestFrame : framer.GetFrame(start + DeltaTime.FromSeconds(_time));

            if (frame != null)
            {
                _renderer.Draw(frame.Draws);
            }
        }

        ImGui.PopFont();

        ImGui.End();
    }
}
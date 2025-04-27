using System.Numerics;
using Cysharp.Text;
using Hexa.NET.ImGui;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Math;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Tyr.Gui.Rendering;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Views;

public class FieldView
{
    private readonly DebugFramer _debugFramer;
    private readonly DebugFilter _filter;
    private readonly DrawableRenderer _renderer = new();
    private readonly Common.Time.Timer _timer = new();

    private Utf8ValueStringBuilder _stringBuilder = ZString.CreateUtf8StringBuilder();
    
    private readonly Subscriber<FieldSize> _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.Latest);
    private FieldSize? _fieldSize;

    private readonly List<Debug.Drawing.Command> _internalDraws = [];

    public FieldView(DebugFramer debugFramer, DebugFilter filter)
    {
        _debugFramer = debugFramer;
        _filter = filter;
        Debug.ModuleContext.Current.Value = ModuleName;

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

            DrawInternals();

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
            _stringBuilder.Clear();
            _stringBuilder.AppendFormat("FPS: {0:F1}", _timer.FpsSmooth);
            ImGui.TextUnformatted(_stringBuilder.AsSpan());
        }

        ImGui.End();
    }

    private void DrawInternals()
    {
        _internalDraws.Clear();

        DrawField();

        _renderer.Draw(_internalDraws, null);
    }

    private void DrawInternal(Debug.Drawing.IDrawable drawable,
        Debug.Drawing.Color color, Debug.Drawing.Options options)
    {
        _internalDraws.Add(new Debug.Drawing.Command(drawable, color, options, Debug.Drawing.Meta.Empty));
    }

    private void DrawField()
    {
        if (_fieldSizeSubscriber.TryGetLatest(out var fieldSize))
        {
            _fieldSize = fieldSize;
        }

        if (!_fieldSize.HasValue) return;

        DrawInternal(new Debug.Drawing.Drawables.Rectangle(_fieldSize.Value.FieldRectangleWithBoundary),
            Debug.Drawing.Color.Green, new Debug.Drawing.Options { Filled = true });

        foreach (var line in _fieldSize.Value.FieldLines)
        {
            DrawInternal(new Debug.Drawing.Drawables.LineSegment(line.LineSegment),
                Debug.Drawing.Color.White, new Debug.Drawing.Options { Thickness = line.Thickness });
        }

        var lineThickness = _fieldSize.Value.LineThickness.GetValueOrDefault();
        DrawInternal(new Debug.Drawing.Drawables.Circle(_fieldSize.Value.CenterCircle),
            Debug.Drawing.Color.White, new Debug.Drawing.Options { Thickness = lineThickness });
    }
}
using System.Numerics;
using Cysharp.Text;
using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Math;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Tyr.Gui.Rendering;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class FieldView : IDisposable
{
    [ConfigEntry] private static float ZoomFactor { get; set; } = 1.1f;

    [ConfigEntry]
    private static Debug.Drawing.Color LineColor { get; set; } = Debug.Drawing.Color.White.WithAlpha(0.7f);

    private readonly DebugFramer _debugFramer;
    private readonly DebugFilter _filter;
    private readonly DrawableRenderer _renderer = new();
    private readonly Common.Time.Timer _timer = new();

    private Utf8ValueStringBuilder _stringBuilder = ZString.CreateUtf8StringBuilder();

    private readonly Subscriber<FieldSize> _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.Latest);
    private FieldSize? _fieldSize;

    private readonly List<Debug.Drawing.Command> _fieldDraws = [];

    public FieldView(DebugFramer debugFramer, DebugFilter filter)
    {
        _debugFramer = debugFramer;
        _filter = filter;

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
                    var newZoom = ImGui.GetIO().MouseWheel > 0
                        ? _renderer.Camera.Zoom * ZoomFactor
                        : _renderer.Camera.Zoom / ZoomFactor;

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

            DrawField();

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

    private void DrawInternal(Debug.Drawing.IDrawable drawable,
        Debug.Drawing.Color color, Debug.Drawing.Options options)
    {
        _fieldDraws.Add(new Debug.Drawing.Command(drawable, color, options, Debug.Meta.Empty, Timestamp.Zero));
    }

    private void DrawField()
    {
        if (_fieldSizeSubscriber.TryGetLatest(out var fieldSize))
        {
            _fieldSize = fieldSize;
            if (!_fieldSize.HasValue) return;

            _fieldDraws.Clear();

            DrawInternal(new Debug.Drawing.Drawables.Rectangle(_fieldSize.Value.FieldRectangleWithBoundary),
                Debug.Drawing.Color.Green800, Debug.Drawing.Options.Filled);

            foreach (var line in _fieldSize.Value.FieldLines)
            {
                DrawInternal(new Debug.Drawing.Drawables.LineSegment(line.LineSegment),
                    LineColor, Debug.Drawing.Options.Outline(line.Thickness));
            }

            foreach (var arc in _fieldSize.Value.FieldArcs)
            {
                var start = Angle.FromRad(arc.A1);
                var end = Angle.FromRad(arc.A2);
                DrawInternal(new Debug.Drawing.Drawables.Arc(arc.Center, arc.Radius, start, end, false),
                    LineColor, Debug.Drawing.Options.Outline(arc.Thickness));
            }
        }

        _renderer.Draw(_fieldDraws, null);
    }

    public void Dispose()
    {
        _stringBuilder.Dispose();
        _fieldSizeSubscriber.Dispose();
    }
}
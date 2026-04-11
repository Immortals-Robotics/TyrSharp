using System.Numerics;
using Cysharp.Text;
using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Debug.Db;
using Tyr.Common.Math;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Tyr.Gui.Rendering;
using Tyr.Soccer;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class FieldView : IDisposable
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Video} Field";
    [ConfigEntry] private static float ZoomFactor { get; set; } = 1.1f;
    [ConfigEntry] private static float ZoomDefault { get; set; } = 0.1f;
    [ConfigEntry] private static float ZoomLimitFactor { get; set; } = 10f;

    [ConfigEntry]
    private static Debug.Drawing.Color LineColor { get; set; } = Debug.Drawing.Color.White.WithAlpha(0.7f);

    private readonly IDebugDb _debugDb;
    private readonly DrawableRenderer _renderer = new();
    private readonly Common.Time.Timer _timer = new();

    private Utf8ValueStringBuilder _stringBuilder = ZString.CreateUtf8StringBuilder();

    private readonly Subscriber<FieldSize> _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.Latest);
    private FieldSize? _fieldSize;

    private readonly List<Debug.Drawing.Command> _fieldDraws = [];

    internal sealed class PreparedModule
    {
        public string ModuleName = string.Empty;
        public List<Debug.Drawing.Command> Commands { get; } = [];
    }

    internal sealed class PreparedData
    {
        private readonly Stack<PreparedModule> _modulePool = [];

        public List<PreparedModule> Modules { get; } = [];

        public void Reset()
        {
            foreach (var module in Modules)
            {
                module.ModuleName = string.Empty;
                module.Commands.Clear();
                _modulePool.Push(module);
            }

            Modules.Clear();
        }

        public PreparedModule AddModule(string moduleName)
        {
            var module = _modulePool.Count > 0 ? _modulePool.Pop() : new PreparedModule();
            module.ModuleName = moduleName;
            Modules.Add(module);
            return module;
        }
    }

    public FieldView(IDebugDb debugDb)
    {
        _debugDb = debugDb;

        _timer.Start();

        _renderer.Camera.Position = Vector2.Zero;
        _renderer.Camera.Zoom = ZoomDefault;
    }

    internal void Prepare(PlaybackTime time, DebugFilterSnapshot filterSnapshot, PreparedData prepared)
    {
        prepared.Reset();

        foreach (var module in _debugDb.QueryModules())
        {
            if (!filterSnapshot.IsEnabled(module))
                continue;

            var frame = time.GetVisibleFrame(_debugDb, module);
            if (!frame.HasValue)
                continue;

            PreparedModule? preparedModule = null;
            foreach (var draw in _debugDb.Query<Debug.Drawing.Command>(
                         module,
                         frame.Value.Start,
                         frame.Value.End))
            {
                if (!filterSnapshot.IsEnabled(draw.Meta))
                    continue;

                preparedModule ??= prepared.AddModule(module);
                preparedModule.Commands.Add(draw);
            }
        }
    }

    internal void Draw(PreparedData prepared, bool isLive)
    {
        _timer.Update();

        if (ImGui.Begin(WindowTitle))
        {
            ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);

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
                    
                    newZoom = Math.Clamp(newZoom, ZoomDefault / ZoomLimitFactor, ZoomDefault * ZoomLimitFactor);

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
                
                // reset
                if (ImGui.IsKeyDown(ImGuiKey.F))
                {
                    _renderer.Camera.Zoom = ZoomDefault;
                    _renderer.Camera.Position = Vector2.Zero;
                }
            }

            DrawField();
            TryCaptureManualTarget(isLive);

            foreach (var module in prepared.Modules)
            {
                _renderer.Draw(module.Commands, null);
            }

            ShowStats();

            ImGui.PopFont();

            if (!isLive)
            {
                // Draw a red border to indicate that we're showing playback data, not live data.
                var min = ImGui.GetWindowPos();
                var max = min + ImGui.GetWindowSize();
                ImGui.GetWindowDrawList().AddRect(min, max, ImGui.ColorConvertFloat4ToU32(Debug.Drawing.Color.Red), 0f, ImDrawFlags.None, 4f);
            }
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
        _fieldDraws.Add(new Debug.Drawing.Command
        {
            Drawable = drawable, Color = color, Options = options, Meta = Debug.Meta.Empty, Timestamp = Timestamp.Zero
        });
    }

    private void DrawField()
    {
        if (_fieldSizeSubscriber.Reader.TryRead(out var fieldSize))
        {
            _fieldSize = fieldSize;
            if (!_fieldSize.HasValue) return;

            _fieldDraws.Clear();

            DrawInternal(new Debug.Drawing.Drawables.Rectangle(_fieldSize.Value.RectangleWithBoundary),
                Debug.Drawing.Color.Green800, Debug.Drawing.Options.Filled);

            foreach (var line in _fieldSize.Value.Lines)
            {
                DrawInternal(new Debug.Drawing.Drawables.LineSegment(line.LineSegment),
                    LineColor, Debug.Drawing.Options.Outline(line.Thickness));
            }

            foreach (var arc in _fieldSize.Value.Arcs)
            {
                DrawInternal(new Debug.Drawing.Drawables.Arc(arc),
                    LineColor, Debug.Drawing.Options.Outline(arc.Thickness));
            }
        }

        _renderer.Draw(_fieldDraws, null);
    }

    private void TryCaptureManualTarget(bool isLive)
    {
        if (!isLive || !ImGui.IsWindowHovered(ImGuiHoveredFlags.None))
        {
            return;
        }

        var team = ManualControlView.SelectedManualTeam;
        if (team == TeamColor.Unknown)
        {
            return;
        }

        var snapshot = TeamRunner.GetManualControlSnapshot(team);
        if (!snapshot.Enabled)
        {
            return;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Right) && !ImGui.IsMouseDragging(ImGuiMouseButton.Right))
        {
            var point = _renderer.Camera.ScreenToWorld(ImGui.GetMousePos());

            if (snapshot.AwaitingLookTarget)
            {
                TeamRunner.SetManualLookTarget(team, point);
            }
            else
            {
                TeamRunner.SetManualTargetPoint(team, point);
            }
        }
    }

    public void Dispose()
    {
        _stringBuilder.Dispose();
        _fieldSizeSubscriber.Dispose();
    }
}

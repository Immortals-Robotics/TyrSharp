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
using Tyr.Gui.Simulator;
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
    [ConfigEntry] private static float FitPaddingFactor { get; set; } = 1.05f;

    [ConfigEntry]
    private static Debug.Drawing.Color LineColor { get; set; } = Debug.Drawing.Color.White.WithAlpha(0.7f);

    [ConfigEntry]
    private static Debug.Drawing.Color BoundaryColor { get; set; } = Debug.Drawing.Color.Green900;

    [ConfigEntry]
    private static Debug.Drawing.Color FieldColor { get; set; } = Debug.Drawing.Color.Green800;

    [ConfigEntry]
    private static Debug.Drawing.Color WallColor { get; set; } = Debug.Drawing.Color.Zinc300.WithAlpha(0.85f);

    private readonly SwitchableDebugDb _debugDb;
    private readonly DrawableRenderer _renderer = new();
    private readonly Common.Time.Timer _timer = new();

    private Utf8ValueStringBuilder _stringBuilder = ZString.CreateUtf8StringBuilder();

    private readonly Subscriber<FieldSize> _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.Latest);
    private FieldSize? _fieldSize = FieldSize.Default;
    private bool _fitCameraPending = true;

    private readonly List<Debug.Drawing.Command> _fieldDraws = [];

    public required SimulatorChannel SimulatorChannel { get; init; }
    private Vector2 _contextMenuWorldPos;

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

    public FieldView(SwitchableDebugDb debugDb)
    {
        _debugDb = debugDb;

        _timer.Start();

        _renderer.Camera.Position = Vector2.Zero;
        _renderer.Camera.Zoom = 1f;
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

            if (_fitCameraPending)
            {
                TryFitCameraToField();
            }

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
                    TryFitCameraToField();
                }
            }

            var viewportOffset = _renderer.Camera.Viewport.Offset;
            var viewportSize = _renderer.Camera.Viewport.Size;
            ImGui.PushClipRect(viewportOffset, viewportOffset + viewportSize, true);

            DrawField();

            foreach (var module in prepared.Modules)
            {
                _renderer.Draw(module.Commands, null, pushClipRect: false);
            }

            ImGui.PopClipRect();

            TryShowContextMenu(isLive);

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
            _stringBuilder.AppendFormat("FPS: {0:F1}\0", _timer.FpsSmooth);
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
        }

        if (!_fieldSize.HasValue)
        {
            _fieldSize = FieldSize.Default;
        }

        var currentField = _fieldSize.Value;

        _fieldDraws.Clear();

        DrawInternal(new Debug.Drawing.Drawables.Rectangle(currentField.RectangleWithBoundary),
            BoundaryColor, Debug.Drawing.Options.Filled);

        DrawInternal(new Debug.Drawing.Drawables.Rectangle(currentField.Rectangle),
            FieldColor, Debug.Drawing.Options.Filled);

        DrawBoundaryWalls(currentField);

        foreach (var line in EnumerateFieldLines(currentField))
        {
            DrawInternal(new Debug.Drawing.Drawables.LineSegment(line.LineSegment),
                LineColor, Debug.Drawing.Options.Outline(line.Thickness));
        }

        foreach (var arc in EnumerateFieldArcs(currentField))
        {
            DrawInternal(new Debug.Drawing.Drawables.Arc(arc),
                LineColor, Debug.Drawing.Options.Outline(arc.Thickness));
        }

        _renderer.Draw(_fieldDraws, null);
    }

    private void TryFitCameraToField()
    {
        var viewport = _renderer.Camera.Viewport;
        if (viewport.Size.X <= 0f || viewport.Size.Y <= 0f)
        {
            return;
        }

        var fieldBounds = GetFieldContentBounds(_fieldSize ?? FieldSize.Default);
        var paddedSize = fieldBounds.Size * FitPaddingFactor;
        if (paddedSize.X <= 0f || paddedSize.Y <= 0f)
        {
            return;
        }

        var zoomX = viewport.Size.X / paddedSize.X;
        var zoomY = viewport.Size.Y / paddedSize.Y;
        var fitZoom = MathF.Min(zoomX, zoomY);
        var minZoom = ZoomDefault / ZoomLimitFactor;
        var maxZoom = ZoomDefault * ZoomLimitFactor;

        _renderer.Camera.Position = fieldBounds.Center;
        _renderer.Camera.Zoom = Math.Clamp(fitZoom, minZoom, maxZoom);
        _fitCameraPending = false;
    }

    private static Common.Math.Shapes.Rectangle GetFieldContentBounds(FieldSize fieldSize)
    {
        var halfFieldWidth = fieldSize.FieldLength / 2f;
        var halfFieldHeight = fieldSize.FieldWidth / 2f;
        var goalReach = MathF.Max(fieldSize.BoundaryWidthGoalLine, fieldSize.GoalDepth);

        return Common.Math.Shapes.Rectangle.FromCenterAndSize(
            Vector2.Zero,
            fieldSize.FieldLength + 2f * goalReach,
            fieldSize.FieldWidth + 2f * fieldSize.BoundaryWidth);
    }

    private void DrawBoundaryWalls(FieldSize fieldSize)
    {
        var halfFieldWidth = fieldSize.FieldLength / 2f;
        var halfFieldHeight = fieldSize.FieldWidth / 2f;
        var halfGoalWidth = fieldSize.GoalWidth / 2f;

        var leftWallX = -halfFieldWidth - fieldSize.BoundaryWidthGoalLine;
        var rightWallX = halfFieldWidth + fieldSize.BoundaryWidthGoalLine;
        var topWallY = halfFieldHeight + fieldSize.BoundaryWidth;
        var bottomWallY = -halfFieldHeight - fieldSize.BoundaryWidth;

        var wallThickness = MathF.Max(fieldSize.LineThickness, 30f);

        DrawWallSegment(new Vector2(leftWallX, bottomWallY), new Vector2(leftWallX, -halfGoalWidth), wallThickness);
        DrawWallSegment(new Vector2(leftWallX, halfGoalWidth), new Vector2(leftWallX, topWallY), wallThickness);
        DrawWallSegment(new Vector2(rightWallX, bottomWallY), new Vector2(rightWallX, -halfGoalWidth), wallThickness);
        DrawWallSegment(new Vector2(rightWallX, halfGoalWidth), new Vector2(rightWallX, topWallY), wallThickness);

        DrawWallSegment(new Vector2(leftWallX, topWallY), new Vector2(rightWallX, topWallY), wallThickness);
        DrawWallSegment(new Vector2(leftWallX, bottomWallY), new Vector2(rightWallX, bottomWallY), wallThickness);

        var leftGoalBackX = -halfFieldWidth - fieldSize.GoalDepth;
        var rightGoalBackX = halfFieldWidth + fieldSize.GoalDepth;

        DrawWallSegment(new Vector2(-halfFieldWidth, -halfGoalWidth), new Vector2(leftGoalBackX, -halfGoalWidth), wallThickness);
        DrawWallSegment(new Vector2(-halfFieldWidth, halfGoalWidth), new Vector2(leftGoalBackX, halfGoalWidth), wallThickness);
        DrawWallSegment(new Vector2(leftGoalBackX, -halfGoalWidth), new Vector2(leftGoalBackX, halfGoalWidth), wallThickness);

        DrawWallSegment(new Vector2(halfFieldWidth, -halfGoalWidth), new Vector2(rightGoalBackX, -halfGoalWidth), wallThickness);
        DrawWallSegment(new Vector2(halfFieldWidth, halfGoalWidth), new Vector2(rightGoalBackX, halfGoalWidth), wallThickness);
        DrawWallSegment(new Vector2(rightGoalBackX, -halfGoalWidth), new Vector2(rightGoalBackX, halfGoalWidth), wallThickness);
    }

    private void DrawWallSegment(Vector2 start, Vector2 end, float thickness)
    {
        DrawInternal(new Debug.Drawing.Drawables.LineSegment(new Common.Math.Shapes.LineSegment
            { Start = start, End = end }),
            WallColor,
            Debug.Drawing.Options.Outline(thickness));
    }

    private static IEnumerable<FieldLineSegment> EnumerateFieldLines(FieldSize fieldSize)
    {
        if (fieldSize.Lines.Count > 0)
        {
            return fieldSize.Lines;
        }

        var thickness = fieldSize.LineThickness;
        var halfFieldWidth = fieldSize.FieldLength / 2f;
        var halfFieldHeight = fieldSize.FieldWidth / 2f;
        var halfPenaltyWidth = fieldSize.PenaltyAreaWidth / 2f;
        var penaltyDepth = fieldSize.PenaltyAreaDepth;

        return
        [
            new FieldLineSegment
            {
                Name = "TopTouchLine",
                Type = FieldShapeType.TopTouchLine,
                P1 = new Vector2(-halfFieldWidth, halfFieldHeight),
                P2 = new Vector2(halfFieldWidth, halfFieldHeight),
                Thickness = thickness
            },
            new FieldLineSegment
            {
                Name = "BottomTouchLine",
                Type = FieldShapeType.BottomTouchLine,
                P1 = new Vector2(-halfFieldWidth, -halfFieldHeight),
                P2 = new Vector2(halfFieldWidth, -halfFieldHeight),
                Thickness = thickness
            },
            new FieldLineSegment
            {
                Name = "LeftGoalLine",
                Type = FieldShapeType.LeftGoalLine,
                P1 = new Vector2(-halfFieldWidth, -halfFieldHeight),
                P2 = new Vector2(-halfFieldWidth, halfFieldHeight),
                Thickness = thickness
            },
            new FieldLineSegment
            {
                Name = "RightGoalLine",
                Type = FieldShapeType.RightGoalLine,
                P1 = new Vector2(halfFieldWidth, -halfFieldHeight),
                P2 = new Vector2(halfFieldWidth, halfFieldHeight),
                Thickness = thickness
            },
            new FieldLineSegment
            {
                Name = "HalfwayLine",
                Type = FieldShapeType.HalfwayLine,
                P1 = new Vector2(0, -halfFieldHeight),
                P2 = new Vector2(0, halfFieldHeight),
                Thickness = thickness
            },
            new FieldLineSegment
            {
                Name = "LeftPenaltyStretch",
                Type = FieldShapeType.LeftPenaltyStretch,
                P1 = new Vector2(-halfFieldWidth + penaltyDepth, -halfPenaltyWidth),
                P2 = new Vector2(-halfFieldWidth + penaltyDepth, halfPenaltyWidth),
                Thickness = thickness
            },
            new FieldLineSegment
            {
                Name = "LeftFieldLeftPenaltyStretch",
                Type = FieldShapeType.LeftFieldLeftPenaltyStretch,
                P1 = new Vector2(-halfFieldWidth, halfPenaltyWidth),
                P2 = new Vector2(-halfFieldWidth + penaltyDepth, halfPenaltyWidth),
                Thickness = thickness
            },
            new FieldLineSegment
            {
                Name = "LeftFieldRightPenaltyStretch",
                Type = FieldShapeType.LeftFieldRightPenaltyStretch,
                P1 = new Vector2(-halfFieldWidth, -halfPenaltyWidth),
                P2 = new Vector2(-halfFieldWidth + penaltyDepth, -halfPenaltyWidth),
                Thickness = thickness
            },
            new FieldLineSegment
            {
                Name = "RightPenaltyStretch",
                Type = FieldShapeType.RightPenaltyStretch,
                P1 = new Vector2(halfFieldWidth - penaltyDepth, -halfPenaltyWidth),
                P2 = new Vector2(halfFieldWidth - penaltyDepth, halfPenaltyWidth),
                Thickness = thickness
            },
            new FieldLineSegment
            {
                Name = "RightFieldLeftPenaltyStretch",
                Type = FieldShapeType.RightFieldLeftPenaltyStretch,
                P1 = new Vector2(halfFieldWidth - penaltyDepth, halfPenaltyWidth),
                P2 = new Vector2(halfFieldWidth, halfPenaltyWidth),
                Thickness = thickness
            },
            new FieldLineSegment
            {
                Name = "RightFieldRightPenaltyStretch",
                Type = FieldShapeType.RightFieldRightPenaltyStretch,
                P1 = new Vector2(halfFieldWidth - penaltyDepth, -halfPenaltyWidth),
                P2 = new Vector2(halfFieldWidth, -halfPenaltyWidth),
                Thickness = thickness
            }
        ];
    }

    private static IEnumerable<FieldCircularArc> EnumerateFieldArcs(FieldSize fieldSize)
    {
        if (fieldSize.Arcs.Count > 0)
        {
            return fieldSize.Arcs;
        }

        return
        [
            new FieldCircularArc
            {
                Name = "CenterCircle",
                Type = FieldShapeType.CenterCircle,
                Center = Vector2.Zero,
                Radius = fieldSize.CenterCircleRadius,
                A1 = 0f,
                A2 = 2f * MathF.PI,
                Thickness = fieldSize.LineThickness,
            }
        ];
    }

    private void TryShowContextMenu(bool isLive)
    {
        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.None)
            && ImGui.IsMouseReleased(ImGuiMouseButton.Right)
            && !ImGui.IsMouseDragging(ImGuiMouseButton.Right))
        {
            _contextMenuWorldPos = _renderer.Camera.ScreenToWorld(ImGui.GetMousePos());
            ImGui.OpenPopup("##field_ctx");
        }

        if (ImGui.BeginPopup("##field_ctx"))
        {
            ImGui.TextColored(Debug.Drawing.Color.Zinc400,
                $"{_contextMenuWorldPos.X:F0}, {_contextMenuWorldPos.Y:F0} mm");
            ImGui.Separator();

            var team = ManualControlView.SelectedManualTeam;
            if (isLive && team != TeamColor.Unknown)
            {
                var snapshot = TeamRunner.GetManualControlSnapshot(team);
                if (snapshot.Enabled)
                {
                    if (ImGui.MenuItem($"{IconFonts.FontAwesome6.Crosshairs}  Set Manual Target"))
                    {
                        TeamRunner.SetManualTargetPoint(team, _contextMenuWorldPos);
                    }

                    if (ImGui.MenuItem($"{IconFonts.FontAwesome6.Eye}  Set Manual Look Target"))
                    {
                        TeamRunner.SetManualLookTarget(team, _contextMenuWorldPos);
                    }
                    ImGui.Separator();
                }
            }

            if (SimulatorChannel.SimulatorRunning
                && ImGui.MenuItem($"{IconFonts.FontAwesome6.Football}  Teleport Ball Here"))
                SimulatorChannel.TeleportBall(
                    _contextMenuWorldPos.X / 1000f,
                    _contextMenuWorldPos.Y / 1000f);

            ImGui.EndPopup();
        }
    }

    public void Dispose()
    {
        _stringBuilder.Dispose();
        _fieldSizeSubscriber.Dispose();
    }
}

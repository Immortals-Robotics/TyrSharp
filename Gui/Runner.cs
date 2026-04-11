using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Runner;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Tyr.Gui.Pipeline;
using Tyr.Gui.Views;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui;

[Configurable]
public sealed partial class Runner : IDisposable
{
    [ConfigEntry(StorageType.User)] private static int MaxFps { get; set; } = 0;
    [ConfigEntry(StorageType.User)] private static bool VSync { get; set; } = true;
    [ConfigEntry(StorageType.User)] private static ThreadPriority RunnerPriority { get; set; } = ThreadPriority.Highest;

    [ConfigEntry(StorageType.User)] private static int WindowWidth { get; set; } = 1280;
    [ConfigEntry(StorageType.User)] private static int WindowHeight { get; set; } = 720;

    [ConfigEntry(StorageType.User, false)] private static int? WindowPosX { get; set; }
    [ConfigEntry(StorageType.User, false)] private static int? WindowPosY { get; set; }

    [ConfigEntry(StorageType.User, false)] private static bool WindowMaximized { get; set; }
    [ConfigEntry(StorageType.User, editable: false)] private static string ImGuiIni { get; set; } = "";

    private readonly RunnerSync _runner;
    private bool _defaultLayoutPending;

    // backend
    private readonly SdlWindow _window;
    private readonly ImGuiController _imgui;
    private readonly FontRegistry _fonts;

    // views
    private readonly DebugFilter _filter;
    private readonly LogView _log;
    private readonly FieldView _field;
    private readonly PlotView _plots;
    private readonly PlaybackControl _control;
    private readonly SessionsView _sessions;
    private readonly ConfigsView _configs;
    private readonly GameControllerView _gameController;
    private readonly ManualControlView _manualControl;
    private readonly RobotDebugView _robotDebug;
    private readonly RobotStatusView _robotStatus;
    private SslLogPlayerView? _sslLogPlayerView;
    private Tyr.Vision.SslLogPlayer? _sslLogPlayer;
    private readonly GuiFramePipeline _pipeline;
    private GuiFramePipeline.PreparedFrame _preparedFrame;

    public Runner(PlaybackSessionManager playbackSessions, string projectConfigPath)
    {
        var debugDb = playbackSessions.PlaybackDb;

        // init the backend
        _window = new SdlWindow("Tyr",
            WindowWidth, WindowHeight,
            WindowPosX, WindowPosY,
            WindowMaximized);

        _window.SetVSync(VSync);

        _imgui = new ImGuiController(_window);

        if (ImGuiIni != "")
            _imgui.LoadIniFromMemory(ImGuiIni);
        else
            _defaultLayoutPending = true;

        Style.Apply();

        _fonts = new FontRegistry();
        _imgui.InitializeBackends();

        // init our UI views
        _filter = new DebugFilter(debugDb);
        _log = new LogView(debugDb);
        _field = new FieldView(debugDb);
        _plots = new PlotView(debugDb);
        _sessions = new SessionsView(playbackSessions);
        _control = new PlaybackControl(debugDb, _sessions);
        _configs = new ConfigsView(projectConfigPath);
        _gameController = new GameControllerView();
        _manualControl = new ManualControlView();
        _robotDebug = new RobotDebugView();
        _robotStatus = new RobotStatusView();
        _pipeline = new GuiFramePipeline(PrepareFrame);
        _preparedFrame = new GuiFramePipeline.PreparedFrame();
        var initialRequest = CreatePrepareRequest();
        PrepareFrame(initialRequest, _preparedFrame);
        initialRequest.Dispose();

        // and the runner
        _runner = new RunnerSync(Tick, MaxFps, ModuleName, RunnerPriority);

        Configurable.OnUpdated += _ => OnConfigsChanged();
    }

    public void RegisterSslLogPlayer(Tyr.Vision.SslLogPlayer player)
    {
        _sslLogPlayer = player;
        _sslLogPlayerView = new SslLogPlayerView(player);
    }

    private void OnConfigsChanged()
    {
        _runner.SetPriority(RunnerPriority);
        _window.SetVSync(VSync);
        _window.SetSize(WindowWidth, WindowHeight);
    }

    public void Start()
    {
        _runner.StartOnCurrentThread();
    }

    private bool Tick()
    {
        // update
        _window.PollEvents();

        var (width, height) = _window.GetSize();
        if (width != WindowWidth)
        {
            WindowWidth = width;
            Configurable.MarkChanged(StorageType.User);
        }

        if (height != WindowHeight)
        {
            WindowHeight = height;
            Configurable.MarkChanged(StorageType.User);
        }

        var (x, y) = _window.GetPos();
        if (x != WindowPosX)
        {
            WindowPosX = x;
            Configurable.MarkChanged(StorageType.User);
        }

        if (y != WindowPosY)
        {
            WindowPosY = y;
            Configurable.MarkChanged(StorageType.User);
        }

        var maximized = _window.GetMaximized();
        if (maximized != WindowMaximized)
        {
            WindowMaximized = maximized;
            Configurable.MarkChanged(StorageType.User);
        }

        _sslLogPlayer?.Tick();

        // draw
        _window.Clear(Color.Slate950);
        _imgui.NewFrame();
        if (_defaultLayoutPending)
        {
            DefaultLayout.Apply();
            _defaultLayoutPending = false;
        }
        _pipeline.TrySwapLatest(ref _preparedFrame);

        _configs.Draw();
        _gameController.Draw();
        _manualControl.Draw();
        _robotStatus.Draw();
        _robotDebug.Draw();

        _control.Draw();
        _sessions.Draw();
        _log.Draw(_preparedFrame.Log, _preparedFrame.Time.Live);
        _field.Draw(_preparedFrame.Field, _preparedFrame.Time.Live);
        _plots.Draw(_preparedFrame.Plots, _preparedFrame.Time.Live);
        _filter.Draw();

        _sslLogPlayerView?.Draw();

        _pipeline.Enqueue(CreatePrepareRequest());

        _imgui.Render();
        _window.SwapBuffers();

        if (_imgui.WantsIniSave)
        {
            ImGuiIni = _imgui.SaveIniToMemory();
            Configurable.MarkChanged(StorageType.User);
        }

        if (_window.ShouldClose)
        {
            _runner.Stop();
        }

        return true;
    }

    public void Dispose()
    {
        _pipeline.Dispose();
        _window.Dispose();
        _imgui.Dispose();
        _fonts.Dispose();
        _filter.Dispose();
        _log.Dispose();
        _field.Dispose();
        _gameController.Dispose();
        _robotStatus.Dispose();
        _robotDebug.Dispose();
        _sslLogPlayerView?.Dispose();
    }

    private GuiFramePipeline.PrepareRequest CreatePrepareRequest()
    {
        return new GuiFramePipeline.PrepareRequest(
            _control.Current,
            _filter.Snapshot(),
            _plots.CreatePrepareState());
    }

    private void PrepareFrame(GuiFramePipeline.PrepareRequest request, GuiFramePipeline.PreparedFrame prepared)
    {
        prepared.Time = request.Time;
        _field.Prepare(request.Time, request.FilterSnapshot, prepared.Field);
        _log.Prepare(request.Time, request.FilterSnapshot, prepared.Log);
        _plots.Prepare(request.Time, request.FilterSnapshot, request.PlotState, prepared.Plots);
    }
}

using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Debug.Db;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Runner;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Tyr.Gui.Views;

namespace Tyr.Gui;

[Configurable]
public sealed partial class Runner : IDisposable
{
    [ConfigEntry(StorageType.User)] private static int MaxFps { get; set; } = 0;
    [ConfigEntry(StorageType.User)] private static bool VSync { get; set; } = true;
    [ConfigEntry(StorageType.User)] private static ThreadPriority RunnerPriority { get; set; } = ThreadPriority.Highest;

    [ConfigEntry(StorageType.User)] private static int WindowWidth { get; set; } = 1280;
    [ConfigEntry(StorageType.User)] private static int WindowHeight { get; set; } = 720;

    [ConfigEntry(StorageType.User, false)] private static int WindowPosX { get; set; }
    [ConfigEntry(StorageType.User, false)] private static int WindowPosY { get; set; }

    [ConfigEntry(StorageType.User, false)] private static bool WindowMaximized { get; set; }

    private readonly RunnerSync _runner;

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
    private readonly ConfigsView _configs;
    private readonly GameControllerView _gameController;

    public Runner(IDebugDb debugDb)
    {
        // init the backend
        _window = new SdlWindow("Tyr",
            WindowWidth, WindowHeight,
            WindowPosX, WindowPosY,
            WindowMaximized);

        _window.SetVSync(VSync);

        _imgui = new ImGuiController(_window);

        Style.Apply();

        _fonts = new FontRegistry();
        _imgui.InitializeBackends();

        // init our UI views
        _filter = new DebugFilter(debugDb);
        _log = new LogView(_filter, debugDb);
        _field = new FieldView(_filter, debugDb);
        _plots = new PlotView(_filter, debugDb);
        _control = new PlaybackControl(debugDb);
        _configs = new ConfigsView();
        _gameController = new GameControllerView();

        // and the runner
        _runner = new RunnerSync(Tick, MaxFps, ModuleName, RunnerPriority);

        Configurable.OnUpdated += _ => OnConfigsChanged();
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

        // draw
        _window.Clear(Color.Slate950);
        _imgui.NewFrame();
        DebugDbUsageProfiler.BeginFrame();

        ImGui.ShowDemoWindow();

        _configs.Draw();
        _gameController.Draw();

        _control.Draw();
        var currentPlayback = _control.Current;
        _log.Draw(currentPlayback);
        _field.Draw(currentPlayback);
        _plots.Draw(currentPlayback);
        _filter.Draw();
        DebugDbUsageProfiler.EndFrame();

        _imgui.Render();
        _window.SwapBuffers();

        if (_window.ShouldClose)
        {
            _runner.Stop();
        }

        return true;
    }

    public void Dispose()
    {
        _window.Dispose();
        _imgui.Dispose();
        _fonts.Dispose();
        _filter.Dispose();
        _log.Dispose();
        _field.Dispose();
        _gameController.Dispose();
    }
}

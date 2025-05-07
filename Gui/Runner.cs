using Hexa.NET.ImGui;
using Tyr.Common.Config;
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

    [ConfigEntry(StorageType.User)] private static int WindowWidth { get; set; } = 1280;
    [ConfigEntry(StorageType.User)] private static int WindowHeight { get; set; } = 720;

    [ConfigEntry(StorageType.User, false)] private static int WindowPosX { get; set; }
    [ConfigEntry(StorageType.User, false)] private static int WindowPosY { get; set; }

    [ConfigEntry(StorageType.User, false)] private static bool WindowMaximized { get; set; }

    private readonly RunnerSync _runner;

    // backend
    private readonly GlfwWindow _window;
    private readonly ImGuiController _imgui;
    private readonly FontRegistry _fonts;

    // views
    private readonly DebugFramer _framer;
    private readonly DebugFilter _filter;
    private readonly LogView _log;
    private readonly FieldView _field;
    private readonly PlotView _plots;
    private readonly PlaybackControl _control;
    private readonly ConfigsView _configs;

    public Runner()
    {
        // init the backend
        _window = new GlfwWindow("Tyr",
            WindowWidth, WindowHeight,
            WindowPosX, WindowPosY,
            WindowMaximized);

        _window.SetVSync(VSync);

        _imgui = new ImGuiController(_window);

        Style.Apply();

        _fonts = new FontRegistry();

        // init our UI views
        _framer = new DebugFramer();
        _filter = new DebugFilter(_framer);
        _log = new LogView(_framer, _filter);
        _field = new FieldView(_framer, _filter);
        _plots = new PlotView(_framer, _filter);
        _control = new PlaybackControl(_framer);
        _configs = new ConfigsView();

        // and the runner
        _runner = new RunnerSync(Tick, MaxFps, ModuleName);

        Configurable.OnUpdated += _ => OnConfigsChanged();
    }

    private void OnConfigsChanged()
    {
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
        _framer.Tick();
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

        ImGui.ShowDemoWindow();

        _configs.Draw();

        _control.Draw();
        _log.Draw(_control.Current);
        _field.Draw(_control.Current);
        _plots.Draw(_control.Current);
        _filter.Draw();

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
    }
}
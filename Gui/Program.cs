using Hexa.NET.ImGui;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Tyr.Gui.Views;

Tyr.Common.Debug.ModuleContext.Current.Value = ModuleName;
Tyr.Common.Config.Storage.Initialize(args[0]);

// init the backend
using var window = new GlfwWindow(1280, 720, "Tyr");
using var imgui = new ImGuiController(window);

Style.Apply();

// init our UI stuff
using var fontRegistry = new FontRegistry();

var framer = new DebugFramer();
var filter = new DebugFilter(framer);
var log = new LogView(framer, filter);
var field = new FieldView(framer, filter);
var plots = new PlotView(framer, filter);
var control = new PlaybackControl(framer);
var configs = new ConfigsView();

// init the AI
using var sslVisionPublisher = new Tyr.Vision.SslVisionDataPublisher();
using var gcPublisher = new Tyr.Referee.GcDataPublisher();

using var referee = new Tyr.Referee.Runner();
using var vision = new Tyr.Vision.Runner();

while (window.ShouldClose == false)
{
    // update
    framer.Tick();
    window.PollEvents();

    // draw
    window.Clear(1f, .8f, .75f);
    imgui.NewFrame();

    ImGui.ShowDemoWindow();

    configs.Draw();

    control.Draw();
    log.Draw(control.Current);
    field.Draw(control.Current);
    plots.Draw(control.Current);
    filter.Draw();

    imgui.Render();
    window.SwapBuffers();
}
using Hexa.NET.ImGui;
using IconFonts;
using Tyr.Gui.Backend;
using Tyr.Gui.Views;

Tyr.Common.Config.Storage.Initialize(args[0]);

using var sslVisionPublisher = new Tyr.Vision.SslVisionDataPublisher();
using var gcPublisher = new Tyr.Referee.GcDataPublisher();

using var referee = new Tyr.Referee.Runner();
using var vision = new Tyr.Vision.Runner();

var fieldView = new FieldView();

using var window = new GlfwWindow(1280, 720, "Tyr");
using var imgui = new ImGuiController(window);

var dpiScale = ImGui.GetPlatformIO().Monitors[0].DpiScale;
const float baseFontSize = 15f;
var fontSize = MathF.Floor(baseFontSize * dpiScale);

using var fontLoader = new FontLoader(fontSize);

// main font
fontLoader
    .Add("Fonts/InterVariable.ttf") // base
    .Add("Fonts/seguiemj.ttf", (0x1F300, 0x1FAD0)) // emoji
    .Load();

// mono-space font
fontLoader
    .Add("Fonts/JetBrainsMono[wght].ttf")
    .Load();

// icons font
fontLoader
    .Add("Fonts/fa-solid-900.ttf", (FontAwesome6.IconMin, FontAwesome6.IconMax))
    .Load();

ImGui.GetStyle().ScaleAllSizes(dpiScale);

while (window.ShouldClose == false)
{
    window.PollEvents();

    window.Clear(1f, .8f, .75f);
    imgui.NewFrame();

    ImGui.ShowDemoWindow();

    fieldView.Draw();

    imgui.Render();
    window.SwapBuffers();
}
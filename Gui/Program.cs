using System.Numerics;
using Hexa.NET.ImGui;
using Tyr.Gui;
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

var style = ImGui.GetStyle();
style.FramePadding = new Vector2(6f, 3f);
style.FrameRounding = 4f;
style.ItemSpacing = new Vector2(10f, 6f);
style.ScrollbarSize = 15f;

using var fontRegistry = new FontRegistry();

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
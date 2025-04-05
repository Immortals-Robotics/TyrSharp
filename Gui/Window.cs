using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets;

namespace Tyr.Gui;

public class Window : ImWindow
{
    public override string Name => "Window";

    public override void DrawContent()
    {
        ImGui.Begin("hoy");
        ImGui.Text("Hello, World!");
        ImGui.End();
    }
}
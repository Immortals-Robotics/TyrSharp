using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets;
using Hexa.NET.Mathematics;

namespace Tyr.Gui;

public class Window : ImWindow
{
    public override string Name => "Window";

    public override void DrawContent()
    {
        ImGui.Begin("Hippos");
        
        var drawList = ImGui.GetWindowDrawList();

        var p = ImGui.GetCursorScreenPos() + new Vector2(40f, 40f);
        var col32 = Colors.Red.ToUIntRGBA();
        var size = 50f;
        drawList.AddCircle(p + new Vector2(size, size) * 0.5f, size, col32, 40, 5f);
        
        ImGui.End();
    }
}
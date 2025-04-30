using System.Numerics;
using Hexa.NET.ImGui;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Backend;

public static class Style
{
    public static void Apply()
    {
        var style = ImGui.GetStyle();
        style.WindowPadding = new Vector2(8.00f, 8.00f);
        style.FramePadding = new Vector2(5.00f, 2.00f);
        style.CellPadding = new Vector2(6.00f, 6.00f);
        style.ItemSpacing = new Vector2(6.00f, 6.00f);
        style.ItemInnerSpacing = new Vector2(6.00f, 6.00f);
        style.TouchExtraPadding = new Vector2(0.00f, 0.00f);
        style.IndentSpacing = 25;
        style.ScrollbarSize = 15;
        style.GrabMinSize = 10;
        style.WindowBorderSize = 1;
        style.ChildBorderSize = 1;
        style.PopupBorderSize = 1;
        style.FrameBorderSize = 1;
        style.TabBorderSize = 1;
        style.WindowRounding = 7;
        style.ChildRounding = 4;
        style.FrameRounding = 3;
        style.PopupRounding = 4;
        style.ScrollbarRounding = 9;
        style.GrabRounding = 3;
        style.LogSliderDeadzone = 4;
        style.TabRounding = 4;

        var colors = ImGui.GetStyle().Colors;
        colors[(int)ImGuiCol.Text] = Color.White;
        colors[(int)ImGuiCol.TextDisabled] = Color.Grey600;
        colors[(int)ImGuiCol.WindowBg] = Color.Grey900;
        colors[(int)ImGuiCol.ChildBg] = Color.Invisible;
        colors[(int)ImGuiCol.PopupBg] = Color.Grey800;
        colors[(int)ImGuiCol.Border] = Color.Grey900.WithAlpha(0.29f);
        colors[(int)ImGuiCol.BorderShadow] = Color.Black.WithAlpha(0.24f);
        colors[(int)ImGuiCol.FrameBg] = Color.Black.WithAlpha(0.34f);
        colors[(int)ImGuiCol.FrameBgHovered] = Color.Black.WithAlpha(0.54f);
        colors[(int)ImGuiCol.FrameBgActive] = Color.Grey800;
        colors[(int)ImGuiCol.TitleBg] = Color.Black.WithAlpha(0.15f);
        colors[(int)ImGuiCol.TitleBgActive] = Color.Black.WithAlpha(0.24f);
        colors[(int)ImGuiCol.TitleBgCollapsed] = Color.Black;
        colors[(int)ImGuiCol.MenuBarBg] = Color.Grey900;
        colors[(int)ImGuiCol.ScrollbarBg] = Color.Black.WithAlpha(0.54f);
        colors[(int)ImGuiCol.ScrollbarGrab] = Color.Grey700.WithAlpha(0.54f);
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = Color.Grey600.WithAlpha(0.54f);
        colors[(int)ImGuiCol.ScrollbarGrabActive] = Color.Grey600.WithAlpha(0.54f);
        colors[(int)ImGuiCol.CheckMark] = Color.LightBlue500;
        colors[(int)ImGuiCol.SliderGrab] = Color.Grey700.WithAlpha(0.54f);
        colors[(int)ImGuiCol.SliderGrabActive] = Color.Grey600.WithAlpha(0.54f);
        colors[(int)ImGuiCol.Button] = Color.Black.WithAlpha(0.44f);
        colors[(int)ImGuiCol.ButtonHovered] = Color.Grey900.WithAlpha(0.54f);
        colors[(int)ImGuiCol.ButtonActive] = Color.BlueGrey900;
        colors[(int)ImGuiCol.Header] = Color.Black.WithAlpha(0.52f);
        colors[(int)ImGuiCol.HeaderHovered] = Color.Black.WithAlpha(0.36f);
        colors[(int)ImGuiCol.HeaderActive] = Color.BlueGrey900.WithAlpha(0.33f);
        colors[(int)ImGuiCol.Separator] = Color.Grey800;
        colors[(int)ImGuiCol.SeparatorHovered] = Color.Grey700;
        colors[(int)ImGuiCol.SeparatorActive] = Color.Grey600;
        colors[(int)ImGuiCol.ResizeGrip] = Color.Grey800;
        colors[(int)ImGuiCol.ResizeGripHovered] = Color.Grey600;
        colors[(int)ImGuiCol.ResizeGripActive] = Color.Grey600;
        colors[(int)ImGuiCol.Tab] = Color.Grey800.WithAlpha(0.35f);
        colors[(int)ImGuiCol.TabHovered] = Color.Grey800;
        colors[(int)ImGuiCol.TabSelected] = Color.Grey800.WithAlpha(0.7f);
        colors[(int)ImGuiCol.TabDimmed] = Color.Grey800.WithAlpha(0.35f);
        colors[(int)ImGuiCol.TabDimmedSelected] = Color.Grey800.WithAlpha(0.7f);
        colors[(int)ImGuiCol.DockingPreview] = Color.LightBlue400;
        colors[(int)ImGuiCol.DockingEmptyBg] = Color.BlueGrey900;
        colors[(int)ImGuiCol.PlotLines] = Color.Red500;
        colors[(int)ImGuiCol.PlotLinesHovered] = Color.Red500;
        colors[(int)ImGuiCol.PlotHistogram] = Color.Red500;
        colors[(int)ImGuiCol.PlotHistogramHovered] = Color.Red500;
        colors[(int)ImGuiCol.TableHeaderBg] = Color.Black.WithAlpha(0.42f);
        colors[(int)ImGuiCol.TableBorderStrong] = Color.Grey700;
        colors[(int)ImGuiCol.TableBorderLight] = Color.Grey800;
        colors[(int)ImGuiCol.TableRowBg] = Color.Grey900.WithAlpha(0.5f);
        colors[(int)ImGuiCol.TableRowBgAlt] = Color.Grey800.WithAlpha(0.5f);
        colors[(int)ImGuiCol.TextSelectedBg] = Color.BlueGrey900;
        colors[(int)ImGuiCol.DragDropTarget] = Color.LightBlue400;
        colors[(int)ImGuiCol.NavCursor] = Color.Red500;
        colors[(int)ImGuiCol.NavWindowingHighlight] = Color.Red500.WithAlpha(0.70f);
        colors[(int)ImGuiCol.NavWindowingDimBg] = Color.Red500.WithAlpha(0.20f);
        colors[(int)ImGuiCol.ModalWindowDimBg] = Color.Invisible;
    }
}
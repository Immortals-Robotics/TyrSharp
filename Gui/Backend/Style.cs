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
        style.GrabMinSize = 12;
        style.WindowBorderSize = 0;
        style.ChildBorderSize = 0;
        style.PopupBorderSize = 0f;
        style.FrameBorderSize = 0f;
        style.TabBorderSize = 1f;
        style.TabBarBorderSize = 1f;
        style.TabBarOverlineSize = 1f;
        style.WindowRounding = 7;
        style.ChildRounding = 4;
        style.FrameRounding = 3;
        style.PopupRounding = 4;
        style.ScrollbarRounding = 9;
        style.GrabRounding = 3;
        style.LogSliderDeadzone = 4;
        style.TabRounding = 4;
        style.DockingSeparatorSize = 2f;

        var colors = ImGui.GetStyle().Colors;
        colors[(int)ImGuiCol.Text] = Color.White;
        colors[(int)ImGuiCol.TextDisabled] = Color.Zinc500;
        colors[(int)ImGuiCol.WindowBg] = Color.Zinc900;
        colors[(int)ImGuiCol.ChildBg] = Color.Invisible;
        colors[(int)ImGuiCol.PopupBg] = Color.Zinc800;
        colors[(int)ImGuiCol.Border] = Color.Zinc700.WithAlpha(0.5f);
        colors[(int)ImGuiCol.BorderShadow] = Color.Zinc950.WithAlpha(0.24f);
        colors[(int)ImGuiCol.FrameBg] = Color.Zinc950.WithAlpha(0.35f);
        colors[(int)ImGuiCol.FrameBgHovered] = Color.Zinc800.WithAlpha(0.5f);
        colors[(int)ImGuiCol.FrameBgActive] = Color.Zinc800;
        colors[(int)ImGuiCol.TitleBg] = Color.Zinc950;
        colors[(int)ImGuiCol.TitleBgActive] = Color.Zinc950.WithAlpha(0.75f);
        colors[(int)ImGuiCol.TitleBgCollapsed] = Color.Zinc950;
        colors[(int)ImGuiCol.MenuBarBg] = Color.Zinc950.WithAlpha(0.35f);
        colors[(int)ImGuiCol.ScrollbarBg] = Color.Zinc950.WithAlpha(0.4f);
        colors[(int)ImGuiCol.ScrollbarGrab] = Color.Zinc800;
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = Color.Zinc700;
        colors[(int)ImGuiCol.ScrollbarGrabActive] = Color.Zinc600;
        colors[(int)ImGuiCol.CheckMark] = Color.Sky500;
        colors[(int)ImGuiCol.SliderGrab] = Color.Zinc600;
        colors[(int)ImGuiCol.SliderGrabActive] = Color.Zinc500;
        colors[(int)ImGuiCol.Button] = Color.Zinc800;
        colors[(int)ImGuiCol.ButtonHovered] = Color.Zinc700;
        colors[(int)ImGuiCol.ButtonActive] = Color.Zinc900;
        colors[(int)ImGuiCol.Header] = Color.Zinc800;
        colors[(int)ImGuiCol.HeaderHovered] = Color.Zinc700;
        colors[(int)ImGuiCol.HeaderActive] = Color.Slate900.WithAlpha(0.33f);
        colors[(int)ImGuiCol.Separator] = Color.Zinc800;
        colors[(int)ImGuiCol.SeparatorHovered] = Color.Zinc700;
        colors[(int)ImGuiCol.SeparatorActive] = Color.Zinc600;
        colors[(int)ImGuiCol.ResizeGrip] = Color.Zinc800;
        colors[(int)ImGuiCol.ResizeGripHovered] = Color.Zinc600;
        colors[(int)ImGuiCol.ResizeGripActive] = Color.Zinc600;
        colors[(int)ImGuiCol.Tab] = Color.Zinc900;
        colors[(int)ImGuiCol.TabHovered] = Color.Zinc700;
        colors[(int)ImGuiCol.TabSelected] = Color.Zinc800;
        colors[(int)ImGuiCol.TabSelectedOverline] = Color.Zinc700;
        colors[(int)ImGuiCol.TabDimmed] = Color.Zinc900;
        colors[(int)ImGuiCol.TabDimmedSelected] = Color.Zinc800;
        colors[(int)ImGuiCol.TabDimmedSelectedOverline] = Color.Invisible;
        colors[(int)ImGuiCol.DockingPreview] = Color.Sky500;
        colors[(int)ImGuiCol.DockingEmptyBg] = Color.Sky950;
        colors[(int)ImGuiCol.PlotLines] = Color.Red500;
        colors[(int)ImGuiCol.PlotLinesHovered] = Color.Red500;
        colors[(int)ImGuiCol.PlotHistogram] = Color.Red500;
        colors[(int)ImGuiCol.PlotHistogramHovered] = Color.Red500;
        colors[(int)ImGuiCol.TableHeaderBg] = Color.Zinc950.WithAlpha(0.4f);
        colors[(int)ImGuiCol.TableBorderStrong] = Color.Zinc700;
        colors[(int)ImGuiCol.TableBorderLight] = Color.Zinc800;
        colors[(int)ImGuiCol.TableRowBg] = Color.Zinc900;
        colors[(int)ImGuiCol.TableRowBgAlt] = Color.Zinc800;
        colors[(int)ImGuiCol.TextSelectedBg] = Color.Slate700;
        colors[(int)ImGuiCol.DragDropTarget] = Color.Sky400;
        colors[(int)ImGuiCol.NavCursor] = Color.Red500;
        colors[(int)ImGuiCol.NavWindowingHighlight] = Color.Red500.WithAlpha(0.70f);
        colors[(int)ImGuiCol.NavWindowingDimBg] = Color.Red500.WithAlpha(0.20f);
        colors[(int)ImGuiCol.ModalWindowDimBg] = Color.Invisible;
    }
}
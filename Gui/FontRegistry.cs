using Hexa.NET.ImGui;
using IconFonts;
using Tyr.Common.Config;
using Tyr.Gui.Backend;

namespace Tyr.Gui;

[Configurable]
public sealed class FontRegistry : IDisposable
{
    [ConfigEntry] private static float FontSize { get; set; } = 15f;

    public static FontRegistry Instance { get; private set; } = null!;

    public ImFontPtr UiFont { get; }
    public ImFontPtr MonoFont { get; }
    public ImFontPtr IconFont { get; }

    private readonly FontLoader _loader;

    public FontRegistry()
    {
        var dpiScale = ImGui.GetPlatformIO().Monitors[0].DpiScale;
        _loader = new FontLoader();

        UiFont = _loader
            .Add("Fonts/InterVariable.ttf", null, FontSize, dpiScale) // base
            .Add("Fonts/seguiemj.ttf", (0x1F300, 0x1FAD0), FontSize, dpiScale) // emoji
            .Load();

        MonoFont = _loader
            .Add("Fonts/JetBrainsMono[wght].ttf", null, FontSize * 1.1f, dpiScale * 2f)
            .Load();

        IconFont = _loader
            .Add("Fonts/fa-solid-900.ttf", (FontAwesome6.IconMin, FontAwesome6.IconMax), FontSize, dpiScale)
            .Load();

        Assert.IsNull(Instance);
        Instance = this;
    }

    public void Dispose()
    {
        _loader.Dispose();
    }
}
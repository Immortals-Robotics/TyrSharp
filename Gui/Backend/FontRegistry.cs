using System.Numerics;
using Hexa.NET.ImGui;
using IconFonts;
using Tyr.Common.Config;

namespace Tyr.Gui.Backend;

[Configurable]
public sealed class FontRegistry : IDisposable
{
    [ConfigEntry] private static float FontSize { get; set; } = 17f;

    public static FontRegistry Instance { get; private set; } = null!;

    public ImFontPtr UiFont { get; }
    public ImFontPtr MonoFont { get; }

    private readonly FontLoader _loader;

    public FontRegistry()
    {
        var dpiScale = ImGui.GetPlatformIO().Monitors[0].DpiScale;
        _loader = new FontLoader();

        var monoScale = 1.1f;
        var emojiScale = 0.85f;
        var emojiOffset = new Vector2(-2.5f, 0f) * (FontSize / 17f);
        var iconsScale = 0.85f;

        UiFont = _loader
            .Add("Fonts/InterVariable.ttf", null, FontSize, dpiScale) // base
            .Add("Fonts/seguiemj.ttf", (0x1F300, 0x1FAD0), FontSize * emojiScale, dpiScale, emojiOffset) // emoji
            .Add("Fonts/fa-solid-900.ttf", (FontAwesome6.IconMin, FontAwesome6.IconMax), FontSize * iconsScale,
                dpiScale) // icons
            .Load();

        MonoFont = _loader
            .Add("Fonts/JetBrainsMono[wght].ttf", null, FontSize * monoScale, dpiScale * 2f)
            .Add("Fonts/seguiemj.ttf", (0x1F300, 0x1FAD0), FontSize * emojiScale, dpiScale, emojiOffset) // emoji
            .Add("Fonts/fa-solid-900.ttf", (FontAwesome6.IconMin, FontAwesome6.IconMax), FontSize * iconsScale,
                dpiScale) // icons
            .Load();

        ImGui.GetIO().FontDefault = UiFont;

        Assert.IsNull(Instance);
        Instance = this;
    }

    public void Dispose()
    {
        _loader.Dispose();
    }
}
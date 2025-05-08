using System.Numerics;
using Hexa.NET.ImGui;
using IconFonts;
using Tyr.Common.Config;

namespace Tyr.Gui.Backend;

[Configurable]
public sealed partial class FontRegistry : IDisposable
{
    [ConfigEntry] private static int FontSize { get; set; } = 17;

    [ConfigEntry] private static int FieldFontMinSize { get; set; } = 10;
    [ConfigEntry] private static int FieldFontMaxSize { get; set; } = 100;
    [ConfigEntry] private static int FieldFontSizeStep { get; set; } = 5;

    public static FontRegistry Instance { get; private set; } = null!;

    private const string UiFontFile = "Fonts/InterVariable.ttf";
    private const string? EmojiFontFile = "Fonts/seguiemj.ttf";
    private const string? IconsFontFile = "Fonts/fa-solid-900.ttf";
    private const string? MonoFontFile = "Fonts/JetBrainsMono[wght].ttf";

    public ImFontPtr UiFont { get; }
    public ImFontPtr MonoFont { get; }

    public ImFontPtr GetFieldFont(float size)
    {
        var sizeInt = int.Clamp((int)Math.Round(size), FieldFontMinSize, FieldFontMaxSize);
        var index = int.Clamp((sizeInt - FieldFontMinSize) / FieldFontSizeStep, 0, _fieldFonts.Count - 1);
        return _fieldFonts[index];
    }

    private readonly FontLoader _loader;

    private readonly List<ImFontPtr> _fieldFonts = [];

    public FontRegistry()
    {
        var dpiScale = ImGui.GetPlatformIO().Monitors[0].DpiScale;
        _loader = new FontLoader();

        var monoScale = 1.1f;
        var emojiScale = 0.85f;
        var emojiOffset = new Vector2(-2.5f, 0f) * (FontSize / 17f);
        var iconsScale = 0.85f;

        UiFont = _loader
            .Add(UiFontFile, null, FontSize, dpiScale) // base
            .Add(EmojiFontFile, (0x1F300, 0x1FAD0), FontSize * emojiScale, dpiScale, emojiOffset) // emoji
            .Add(IconsFontFile, (FontAwesome6.IconMin, FontAwesome6.IconMax), FontSize * iconsScale,
                dpiScale) // icons
            .Load();

        MonoFont = _loader
            .Add(MonoFontFile, null, FontSize * monoScale, dpiScale)
            .Add(IconsFontFile, (FontAwesome6.IconMin, FontAwesome6.IconMax), FontSize * iconsScale,
                dpiScale) // icons
            .Load();

        ImGui.GetIO().FontDefault = UiFont;

        // load field fonts
        for (var size = FieldFontMinSize; size <= FieldFontMaxSize; size += FieldFontSizeStep)
        {
            var font = _loader
                .Add(MonoFontFile, null, size, dpiScale)
                .Load();
            _fieldFonts.Add(font);
        }

        Assert.IsNull(Instance);
        Instance = this;
    }

    public void Dispose()
    {
        _loader.Dispose();
    }
}
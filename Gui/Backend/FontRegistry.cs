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
    [ConfigEntry] private static int FieldFontCount { get; set; } = 10;
    [ConfigEntry] private static float FontSizeDistributionPow { get; set; } = 0.5f;

    public static FontRegistry Instance { get; private set; } = null!;

    private const string UiFontFile = "Fonts/InterVariable.ttf";
    private const string? EmojiFontFile = "Fonts/seguiemj.ttf";
    private const string? IconsFontFile = "Fonts/fa-solid-900.ttf";
    private const string? MonoFontFile = "Fonts/JetBrainsMono[wght].ttf";

    public ImFontPtr UiFont { get; }
    public ImFontPtr MonoFont { get; }

    /// returns a font and a size to use to render with it, given a target font
    public (ImFontPtr, float) GetFieldFont(float size)
    {
        var font = _fieldFonts[GetFontIndex(size)];

        // adjust the size to get a constant text width at various sizes
        // this offsets the variable font width / height ratio
        var correctedSize = size * font.FontSize / (FieldFontAverageHeightToWidthRatio * font.FallbackAdvanceX);

        return (font, correctedSize);
    }

    private float FieldFontAverageHeightToWidthRatio { get; }

    private int GetFontIndex(float size)
    {
        size = float.Clamp(size, FieldFontMinSize, FieldFontMaxSize);
        var normalizedSize = (size - FieldFontMinSize) / (FieldFontMaxSize - FieldFontMinSize);

        var scaledSize = MathF.Pow(normalizedSize, FontSizeDistributionPow);
        var index = (int)MathF.Round(scaledSize * (FieldFontCount - 1));
        return int.Clamp(index, 0, _fieldFonts.Count - 1);
    }

    private static float GetFontSize(int index)
    {
        var normalizedIndex = index / (float)(FieldFontCount - 1);
        var scaledIndex = MathF.Pow(normalizedIndex, 1.0f / FontSizeDistributionPow); // Inverse of the power used above
        var size = FieldFontMinSize + scaledIndex * (FieldFontMaxSize - FieldFontMinSize);
        return MathF.Round(size);
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
        for (var idx = 0; idx < FieldFontCount; idx += 1)
        {
            var size = GetFontSize(idx);
            var font = _loader
                .Add(MonoFontFile, null, size, dpiScale)
                .Load();
            _fieldFonts.Add(font);

            FieldFontAverageHeightToWidthRatio += font.FontSize / font.FallbackAdvanceX;
        }

        FieldFontAverageHeightToWidthRatio /= FieldFontCount;

        Assert.IsNull(Instance);
        Instance = this;
    }

    public void Dispose()
    {
        _loader.Dispose();
    }
}
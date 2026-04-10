using System.Numerics;
using Hexa.NET.ImGui;
using IconFonts;
using Tyr.Common.Config;

namespace Tyr.Gui.Backend;

[Configurable]
public sealed partial class FontRegistry : IDisposable
{
    [ConfigEntry] private static int FontSize { get; set; } = 17;

    public static FontRegistry Instance { get; private set; } = null!;

    private const string UiFontFile = "Fonts/InterVariable.ttf";
    private const string EmojiFontFile = "Fonts/seguiemj.ttf";
    private const string IconsFontFile = "Fonts/fa-solid-900.ttf";
    private const string MonoFontFile = "Fonts/JetBrainsMono[wght].ttf";
    private const float FieldFontMinSampleSize = 10f;
    private const float FieldFontMaxSampleSize = 100f;
    private const int FieldFontSampleCount = 5;
    private readonly FontLoader _loader;

    public ImFontPtr UiFont { get; }
    public ImFontPtr MonoFont { get; }

    /// returns a font and a size to use to render with it, given a target font
    public unsafe (ImFontPtr, float) GetFieldFont(float size)
    {
        var targetSize = MathF.Max(MathF.Abs(size), 1f);
        ImFontBakedPtr bakedFont = MonoFont.GetFontBaked(targetSize);

        // adjust the size to get a constant text width at various sizes
        // this offsets the variable font width / height ratio
        if (!float.IsFinite(bakedFont.Size) || bakedFont.Size <= 0f ||
            !float.IsFinite(bakedFont.FallbackAdvanceX) || bakedFont.FallbackAdvanceX <= 0f ||
            !float.IsFinite(FieldFontAverageHeightToWidthRatio) || FieldFontAverageHeightToWidthRatio <= 0f)
        {
            return (MonoFont, targetSize);
        }

        var correctedSize = targetSize * bakedFont.Size / (FieldFontAverageHeightToWidthRatio * bakedFont.FallbackAdvanceX);
        if (!float.IsFinite(correctedSize) || correctedSize <= 0f)
        {
            correctedSize = targetSize;
        }

        return (MonoFont, correctedSize);
    }

    private float FieldFontAverageHeightToWidthRatio { get; }

    public FontRegistry()
    {
        _loader = new FontLoader();

        var monoScale = 1.1f;
        var emojiScale = 0.85f;
        var emojiOffset = new Vector2(-2.5f, 0f) * (FontSize / 17f);
        var iconsScale = 0.85f;

        UiFont = _loader
            .Add(UiFontFile, size: FontSize) // base
            .Add(EmojiFontFile, (0x1F300u, 0x1FAD0u), FontSize * emojiScale, offset: emojiOffset,
                loadColor: true) // emoji
            .Add(IconsFontFile, ((uint)FontAwesome6.IconMin, (uint)FontAwesome6.IconMax), FontSize * iconsScale) // icons
            .Load();

        MonoFont = _loader
            .Add(MonoFontFile, size: FontSize * monoScale)
            .Add(IconsFontFile, ((uint)FontAwesome6.IconMin, (uint)FontAwesome6.IconMax), FontSize * iconsScale) // icons
            .Load();

        ImGui.GetIO().FontDefault = UiFont;

        FieldFontAverageHeightToWidthRatio = MeasureAverageFieldFontRatio(MonoFont);

        Assert.IsNull(Instance);
        Instance = this;
    }

    public void Dispose()
    {
        _loader.Dispose();
    }

    private static unsafe float MeasureAverageFieldFontRatio(ImFontPtr font)
    {
        var total = 0f;
        var validSamples = 0;

        for (var idx = 0; idx < FieldFontSampleCount; idx += 1)
        {
            var normalizedIndex = FieldFontSampleCount == 1 ? 0f : idx / (float)(FieldFontSampleCount - 1);
            var sampleSize = FieldFontMinSampleSize + (FieldFontMaxSampleSize - FieldFontMinSampleSize) * normalizedIndex;
            ImFontBakedPtr bakedFont = font.GetFontBaked(sampleSize);
            if (!float.IsFinite(bakedFont.Size) || bakedFont.Size <= 0f ||
                !float.IsFinite(bakedFont.FallbackAdvanceX) || bakedFont.FallbackAdvanceX <= 0f)
            {
                continue;
            }

            total += bakedFont.Size / bakedFont.FallbackAdvanceX;
            validSamples += 1;
        }

        return validSamples == 0 ? 1f : total / validSamples;
    }
}

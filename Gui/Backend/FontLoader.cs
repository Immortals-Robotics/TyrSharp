using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Utilities;

namespace Tyr.Gui.Backend;

public sealed class FontLoader(float size = 15f, float supersampling = 1f) : IDisposable
{
    private readonly List<ImGuiFontBuilder> _builders = [];
    private ImGuiFontBuilder? _currentBuilder;

    public FontLoader Add(string? file, (uint, uint)? glyphRange = null)
    {
        if (_currentBuilder == null)
        {
            _currentBuilder = new ImGuiFontBuilder();
            _builders.Add(_currentBuilder);
        }

        _currentBuilder.Config.RasterizerDensity = supersampling;
        _currentBuilder.Config.FontBuilderFlags |= (uint)ImGuiFreeTypeBuilderFlags.LoadColor;

        if (file == null)
        {
            // Default font doesn't support extended glyphs
            Assert.IsNull(glyphRange);

            _currentBuilder.AddDefaultFont();
            Log.ZLogInformation($"Loaded default font");
        }
        else
        {
            if (glyphRange != null)
            {
                _currentBuilder.AddFontFromFileTTF(file, size,
                    [glyphRange.Value.Item1, glyphRange.Value.Item2]);
            }
            else
            {
                _currentBuilder.AddFontFromFileTTF(file, size);
            }

            Log.ZLogInformation($"Loaded font {file}");
        }

        _currentBuilder.Config.MergeMode = true;

        return this;
    }

    public ImFontPtr Load()
    {
        if (_currentBuilder == null) return null;

        var font = _currentBuilder.Build();
        _currentBuilder = null;
        return font;
    }

    public void Dispose()
    {
        Assert.IsNull(_currentBuilder);

        foreach (var fontBuilder in _builders)
        {
            fontBuilder.Dispose();
        }
    }
}
using System.Numerics;
using System.Reflection;
using Hexa.NET.ImGui;
using System.Runtime.InteropServices;

namespace Tyr.Gui.Backend;

public sealed class FontLoader : IDisposable
{
    private readonly List<nint> _ownedBuffers = [];
    private readonly List<ImFontConfigPtr> _configs = [];
    private ImFontPtr _currentFont = ImFontPtr.Null;
    private bool _hasCurrentFont;

    public unsafe FontLoader Add(string? file, (uint, uint)? glyphRange = null, float size = 15f,
        float supersampling = 1f, Vector2? offset = null, bool loadColor = false)
    {
        var atlas = ImGui.GetIO().Fonts;
        var config = ImGui.ImFontConfig();
        _configs.Add(config);

        config.RasterizerDensity = supersampling;
        config.GlyphOffset = offset ?? Vector2.Zero;
        config.FontLoaderFlags = loadColor ? (uint)ImGuiFreeTypeLoaderFlags.LoadColor : 0u;
        config.MergeMode = _hasCurrentFont;
        config.FontDataOwnedByAtlas = false;

        if (file == null)
        {
            Assert.IsNull(glyphRange);
            var font = ImGui.AddFontDefault(atlas, config);
            if (!_hasCurrentFont)
            {
                _currentFont = font;
                _hasCurrentFont = true;
            }

            Log.ZLogInformation($"Loaded default font");
            return this;
        }

        var fontData = ReadEmbeddedFont(typeof(FontLoader).Assembly, file);
        uint* glyphRanges;
        if (glyphRange is { } range)
        {
            glyphRanges = (uint*)AllocateBuffer(sizeof(uint) * 3);
            glyphRanges[0] = range.Item1;
            glyphRanges[1] = range.Item2;
            glyphRanges[2] = 0;
        }
        else
        {
            glyphRanges = atlas.GetGlyphRangesDefault();
        }

        var loadedFont = ImGui.AddFontFromMemoryTTF(atlas, (void*)fontData.Pointer, fontData.Length, size, config, glyphRanges);
        if (!_hasCurrentFont)
        {
            _currentFont = loadedFont;
            _hasCurrentFont = true;
        }

        Log.ZLogInformation($"Loaded font {file}");
        return this;
    }

    public ImFontPtr Load()
    {
        var font = _currentFont;
        _currentFont = ImFontPtr.Null;
        _hasCurrentFont = false;
        return font;
    }

    public void Dispose()
    {
        foreach (var config in _configs)
        {
            ImGui.Destroy(config);
        }

        foreach (var buffer in _ownedBuffers)
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string GetEmbeddedResourceName(Assembly assembly, string resourcePath)
    {
        return $"{assembly.GetName().Name}.{resourcePath.Replace('\\', '.').Replace('/', '.')}";
    }

    private (nint Pointer, int Length) ReadEmbeddedFont(Assembly assembly, string resourcePath)
    {
        var resourceName = GetEmbeddedResourceName(assembly, resourcePath);
        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.IsNotNull(stream);
        var safeStream = stream!;

        var bytes = new byte[safeStream.Length];
        var read = safeStream.Read(bytes, 0, bytes.Length);
        Assert.AreEqual(bytes.Length, read);

        var pointer = AllocateBuffer(bytes.Length);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);

        return (pointer, bytes.Length);
    }

    private nint AllocateBuffer(int length)
    {
        var pointer = Marshal.AllocHGlobal(length);
        _ownedBuffers.Add(pointer);
        return pointer;
    }
}

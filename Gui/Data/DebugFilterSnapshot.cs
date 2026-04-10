using Cysharp.Text;
using Tyr.Common.Debug;
using StrSpan = System.ReadOnlySpan<char>;

namespace Tyr.Gui.Data;

public sealed class DebugFilterSnapshot : IDisposable
{
    private readonly Dictionary<string, bool> _state;
    private readonly Dictionary<string, bool>.AlternateLookup<StrSpan> _lookup;
    private Utf16ValueStringBuilder _stringBuilder = ZString.CreateStringBuilder();

    public DebugFilterSnapshot(Dictionary<string, bool> state)
    {
        _state = state;
        _lookup = state.GetAlternateLookup<StrSpan>();
    }

    public bool IsEnabled(Meta meta) =>
        IsEnabled(meta.Module, meta.Layer, meta.File, meta.Member, meta.Line);

    public bool IsEnabled(string module, string? layer = null,
        string? file = null, string? member = null, int? line = null)
    {
        _stringBuilder.Clear();
        AppendNormalizedPathPart(module);
        if (!IsEnabledInternal(_stringBuilder.AsSpan())) return false;

        if (layer is null) return true;
        _stringBuilder.Append('/');
        AppendNormalizedPathPart(layer);
        if (!IsEnabledInternal(_stringBuilder.AsSpan())) return false;

        if (file is null) return true;
        _stringBuilder.Append('/');
        AppendNormalizedPathPart(file);
        if (!IsEnabledInternal(_stringBuilder.AsSpan())) return false;

        if (member is null) return true;
        _stringBuilder.Append('/');
        AppendNormalizedPathPart(member);
        if (!IsEnabledInternal(_stringBuilder.AsSpan())) return false;

        if (line is null) return true;
        _stringBuilder.Append('/');
        _stringBuilder.Append(line.Value);
        if (!IsEnabledInternal(_stringBuilder.AsSpan())) return false;

        return true;

        bool IsEnabledInternal(StrSpan path) => !_lookup.TryGetValue(path, out var enabled) || enabled;
    }

    private void AppendNormalizedPathPart(string value)
    {
        if (!value.Contains('.'))
        {
            _stringBuilder.Append(value);
            return;
        }

        foreach (var c in value)
            _stringBuilder.Append(c == '.' ? '_' : c);
    }

    public void Dispose()
    {
        _stringBuilder.Dispose();
    }
}

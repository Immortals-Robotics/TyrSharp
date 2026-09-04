using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using Cysharp.Text;

namespace Tyr.Common.Debug;

/// <summary>
/// Interpolated string handler that builds via a pooled ZString buffer and interns the result.
/// On repeated calls with the same interpolated content the intern table hit is O(1) with no allocation.
/// </summary>
[InterpolatedStringHandler]
public ref struct InternedStringHandler
{
    private Utf16ValueStringBuilder _builder;

    public InternedStringHandler(int literalLength, int formattedCount)
        => _builder = ZString.CreateStringBuilder();

    public void AppendLiteral(string s) => _builder.Append(s);

    public void AppendFormatted<T>(T value) => _builder.Append(value);

    public void AppendFormatted<T>(T value, string? format) where T : IFormattable
        => _builder.Append(value.ToString(format, null));

    public void AppendFormatted(string? value) => _builder.Append(value ?? string.Empty);

    /// <summary>
    /// Returns a permanently cached string for the built content, then disposes the pooled buffer.
    /// Repeated calls with the same content return the same instance without allocating.
    /// </summary>
    internal string ToInternedString()
    {
        var span = _builder.AsSpan();
        var result = InternedStringCache.GetOrAdd(span);
        _builder.Dispose();
        return result;
    }
}

/// <summary>
/// Process-wide cache that maps string content to a single permanent instance.
/// Entries are never evicted, so only feed it strings from a bounded vocabulary
/// (plot ids, draw expressions, log messages).
/// </summary>
internal static class InternedStringCache
{
    private static readonly ConcurrentDictionary<string, string> Cache = new();

    private static readonly ConcurrentDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> SpanLookup =
        Cache.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>
    /// Returns the cached instance whose content equals <paramref name="span"/>.
    /// Allocates only on the first call per unique content.
    /// </summary>
    public static string GetOrAdd(ReadOnlySpan<char> span)
    {
        if (SpanLookup.TryGetValue(span, out var existing))
            return existing;

        var created = new string(span);
        return Cache.GetOrAdd(created, created);
    }

    /// <summary>
    /// Returns a cached interned string for a UTF-8 <paramref name="utf8Span"/>.
    /// Decodes to a stack-allocated char buffer (up to 512 chars) then delegates to
    /// <see cref="GetOrAdd(ReadOnlySpan{char})"/>. Zero heap allocations on cache hit.
    /// </summary>
    public static string GetOrAdd(ReadOnlySpan<byte> utf8Span)
    {
        var maxCharCount = Encoding.UTF8.GetMaxCharCount(utf8Span.Length);
        Span<char> charSpan = maxCharCount <= 512
            ? stackalloc char[maxCharCount]
            : new char[maxCharCount];
        var charCount = Encoding.UTF8.GetChars(utf8Span, charSpan);
        return GetOrAdd(charSpan[..charCount]);
    }
}

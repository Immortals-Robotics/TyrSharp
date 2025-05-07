using System.Collections.Concurrent;

namespace Tyr.Common.Debug;

public record Meta
{
    public const string DebugLayerPrefix = "[debug]";
    
    public string Module { get; }
    public string Layer { get; }
    public string? File { get; }
    public string? Member { get; }
    public int Line { get; }
    public string? Expression { get; }

    private Meta(string module, string layer,
        string? file, string? member, int line, string? expression)
    {
        Module = module;
        Layer = layer;
        File = file;
        Member = member;
        Line = line;
        Expression = expression;
    }

    // Cache for interned Meta instances
    private static readonly ConcurrentDictionary<(string, string, string?, string?, int, string?), Meta> Cache = [];

    // Factory method for getting interned instances
    public static Meta GetOrCreate(
        string module, string? layer = null,
        string? file = null, string? member = null, int line = 0, string? expression = null)
    {
        var key = (module, layer ?? string.Empty, file, member, line, expression);
        return Cache.GetOrAdd(key, k => new Meta(k.Item1, k.Item2, k.Item3, k.Item4, k.Item5, k.Item6));
    }

    public static readonly Meta Empty = GetOrCreate(string.Empty);
}
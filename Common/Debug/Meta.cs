using System.Collections.Concurrent;

namespace Tyr.Common.Debug;

public record Meta
{
    public const string DebugLayerPrefix = "[debug]";
    public static string DebugLayer(string layer) => DebugLayerPrefix + layer;
    public static bool IsDebugLayer(string layer) => layer.StartsWith(DebugLayerPrefix);

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

    private readonly record struct Key(
        string Module,
        string Layer,
        string? File,
        string? Member,
        int Line,
        string? Expression
    );

    // Cache for interned Meta instances
    private static readonly ConcurrentDictionary<Key, Meta> Cache = [];

    // Factory method for getting interned instances
    public static Meta GetOrCreate(
        string module, string? layer = null,
        string? file = null, string? member = null, int line = 0, string? expression = null)
    {
        var key = new Key(module, layer ?? string.Empty, file, member, line, expression);
        return Cache.GetOrAdd(key, k => new Meta(k.Module, k.Layer, k.File, k.Member, k.Line, k.Expression));
    }

    public static readonly Meta Empty = GetOrCreate(string.Empty);
}
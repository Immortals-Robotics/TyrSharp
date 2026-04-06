using System.Collections.Concurrent;
using MemoryPack;

namespace Tyr.Common.Debug;

[MemoryPackable]
public partial record struct Meta
{
    public const string DebugLayerPrefix = "[debug]";
    public static string DebugLayer(string layer) => DebugLayerPrefix + layer;
    public static bool IsDebugLayer(string layer) => layer.StartsWith(DebugLayerPrefix);

    public required string Module   { get; init; }
    public required string Layer    { get; init; }
    public string?         File     { get; init; }
    public string?         Member   { get; init; }
    public int             Line     { get; init; }
    public string?         Expression { get; init; }

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
        return Cache.GetOrAdd(key,
            k => new Meta
            {
                Module = k.Module, Layer = k.Layer, File = k.File, Member = k.Member, Line = k.Line,
                Expression = k.Expression
            });
    }

    public static readonly Meta Empty = GetOrCreate(string.Empty);
}
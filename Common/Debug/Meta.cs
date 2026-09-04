using System.Collections.Concurrent;

namespace Tyr.Common.Debug;

/// <summary>
/// Source-location metadata for a debug entry (module, layer, file, member, line, expression).
/// Instances are interned: <see cref="GetOrCreate"/> returns the same object for the same
/// values, so two metas are equal exactly when they are the same reference, and
/// <see cref="Id"/> is a small dense process-wide integer that can index arrays.
/// </summary>
public sealed class Meta
{
    public const string DebugLayerPrefix = "[debug]";
    public static string DebugLayer(string layer) => DebugLayerPrefix + layer;
    public static bool IsDebugLayer(string layer) => layer.StartsWith(DebugLayerPrefix);

    /// <summary>Dense process-wide id, assigned in creation order starting at 0.</summary>
    public int Id { get; }

    public string  Module     { get; }
    public string  Layer      { get; }
    public string? File       { get; }
    public string? Member     { get; }
    public int     Line       { get; }
    public string? Expression { get; }

    private Meta(int id, in Key key)
    {
        Id = id;
        Module = key.Module;
        Layer = key.Layer;
        File = key.File;
        Member = key.Member;
        Line = key.Line;
        Expression = key.Expression;
    }

    private readonly record struct Key(
        string Module,
        string Layer,
        string? File,
        string? Member,
        int Line,
        string? Expression
    );

    private static readonly ConcurrentDictionary<Key, Meta> Cache = [];
    private static int _nextId = -1;

    /// <summary>Number of distinct metas created so far; every <see cref="Id"/> is below this.</summary>
    public static int Count => Volatile.Read(ref _nextId) + 1;

    public static Meta GetOrCreate(
        string module, string? layer = null,
        string? file = null, string? member = null, int line = 0, string? expression = null)
    {
        var key = new Key(module, layer ?? string.Empty, file, member, line, expression);
        return Cache.TryGetValue(key, out var existing) ? existing : Create(key);
    }

    private static Meta Create(in Key key)
    {
        // GetOrAdd may run the factory more than once under contention; only the winner's
        // id is ever observed, losers are dropped. Ids stay unique, and dense enough to index.
        return Cache.GetOrAdd(key, static k => new Meta(Interlocked.Increment(ref _nextId), k));
    }

    public static readonly Meta Empty = GetOrCreate(string.Empty);

    public override string ToString() => $"{Module}/{Layer}/{File}:{Line} {Member} {Expression}";
}

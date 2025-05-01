using System.Collections.Concurrent;

namespace Tyr.Gui.Data;

public record MetaTreeItem : IComparable<MetaTreeItem>
{
    public enum ItemType
    {
        Log,
        Draw,
        Plot,
    }

    public ItemType Type { get; }
    public int Line { get; }
    public string? Expression { get; }

    private MetaTreeItem(ItemType Type, int Line, string? Expression)
    {
        this.Type = Type;
        this.Line = Line;
        this.Expression = Expression;
    }

    // Cache for interned Meta instances
    private static readonly ConcurrentDictionary<(ItemType, int, string?), MetaTreeItem> Cache = new();

    // Factory method for getting interned instances
    public static MetaTreeItem GetOrCreate(ItemType type, int line, string? expression)
    {
        var key = (Type: type, Line: line, Expression: expression);
        return Cache.GetOrAdd(key, k => new MetaTreeItem(k.Item1, k.Item2, k.Item3));
    }

    public int CompareTo(MetaTreeItem? other)
    {
        if (other == null) return 1; // Non-null is greater than null
        return Line.CompareTo(other.Line);
    }
}
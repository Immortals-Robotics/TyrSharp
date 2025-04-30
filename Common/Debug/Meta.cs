using System.Collections.Concurrent;

namespace Tyr.Common.Debug;

public record Meta
{
    public string ModuleName { get; }
    public string? Expression { get; }
    public string? MemberName { get; }
    public string? FilePath { get; }
    public int LineNumber { get; }

    private Meta(string ModuleName,
        string? Expression,
        string? MemberName,
        string? FilePath,
        int LineNumber)
    {
        this.ModuleName = ModuleName;
        this.Expression = Expression;
        this.MemberName = MemberName;
        this.FilePath = FilePath;
        this.LineNumber = LineNumber;
    }

    // Cache for interned Meta instances
    private static readonly ConcurrentDictionary<(string, string?, string?, string?, int), Meta> Cache = new();

    // Factory method for getting interned instances
    public static Meta GetOrCreate(
        string moduleName,
        string? expression = null,
        string? memberName = null,
        string? filePath = null,
        int lineNumber = 0)
    {
        var key = (moduleName, expression, memberName, filePath, lineNumber);

        return Cache.GetOrAdd(key, k => new Meta(k.Item1, k.Item2, k.Item3, k.Item4, k.Item5));
    }

    public static readonly Meta Empty = new(string.Empty, null, null, null, 0);
}
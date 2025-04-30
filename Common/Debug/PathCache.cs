using System.Collections.Concurrent;

namespace Tyr.Common.Debug;

public static class PathCache
{
    private static readonly ConcurrentDictionary<string, string> FileNameCache = [];

    public static string GetFileName(string? path)
    {
        return string.IsNullOrEmpty(path)
            ? string.Empty
            : FileNameCache.GetOrAdd(path, Path.GetFileName);
    }
}
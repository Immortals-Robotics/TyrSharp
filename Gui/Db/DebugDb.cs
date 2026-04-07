using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MemoryPack;
using Tyr.Common.Debug;

namespace Tyr.Gui.Db;

// ─── Internal fixed-size record stored in the .records mmap ─────────────────

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct InternalRecord
{
    public long Timestamp;
    public int  ModuleId;
    public int  SourceLocationId;
    public int  BlobOffset;
    public int  BlobLength;
}

// ─── Main database ──────────────────────────────────────────────────────────

public sealed class DebugDb : IDebugDb
{
    private readonly string _directory;
    private readonly MappedStringPool _strings;
    private readonly MappedSourceLocationTable _sources;
    private readonly ConcurrentDictionary<Type, Bucket> _buckets = new();

    // Module name → interned string id (for query filtering)
    private readonly ConcurrentDictionary<string, int> _moduleIdCache = new();

    // Single lock for interning (cold path)
    private readonly Lock _internLock = new();

    public DebugDb(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);

        _strings = new MappedStringPool(Path.Combine(directory, "strings.pool"));
        _sources = new MappedSourceLocationTable(Path.Combine(directory, "sources.table"));
        _sources.Reload(_strings);

        // Rebuild module id cache from existing string pool
        // Module strings are interned with all other strings, but we track which
        // string ids correspond to modules via the source location table
        RebuildModuleCache();
    }

    private void RebuildModuleCache()
    {
        var count = _sources.Count;
        for (int i = 0; i < count; i++)
        {
            var isl = _sources.GetInternal(i);
            var moduleName = _strings.Get(isl.ModuleId);
            if (moduleName is not null)
                _moduleIdCache.TryAdd(moduleName, isl.ModuleId);
        }
    }

    /// <summary>
    /// Register a type to ensure its bucket is created/loaded.
    /// Call before using Append/Query for that type.
    /// </summary>
    public DebugDb RegisterType<T>() where T : IEntry
    {
        _buckets.GetOrAdd(typeof(T), _ => new Bucket(_directory, typeof(T).Name));
        return this;
    }

    private Bucket GetOrCreateBucket<T>() where T : IEntry
    {
        return _buckets.GetOrAdd(typeof(T), _ => new Bucket(_directory, typeof(T).Name));
    }

    public void Append<T>(T entry) where T : IEntry
    {
        var bucket = GetOrCreateBucket<T>();

        int sourceId;
        int moduleId;
        lock (_internLock)
        {
            sourceId = _sources.Intern(entry.Meta, _strings);
            moduleId = _strings.Intern(entry.Meta.Module);
        }
        _moduleIdCache.TryAdd(entry.Meta.Module, moduleId);

        var blob = MemoryPackSerializer.Serialize(entry);

        var record = new InternalRecord
        {
            Timestamp        = entry.Timestamp.Nanoseconds,
            ModuleId         = moduleId,
            SourceLocationId = sourceId,
        };

        bucket.Append(record, blob);
    }

    public IEnumerable<T> Query<T>(string module, Timestamp t0, Timestamp t1) where T : IEntry
    {
        if (!_buckets.TryGetValue(typeof(T), out var bucket))
            yield break;

        if (!_moduleIdCache.TryGetValue(module, out var moduleId))
            yield break;

        var count = bucket.RecordCount;
        var lo = bucket.LowerBound(t0.Nanoseconds, count);
        var hi = bucket.UpperBound(t1.Nanoseconds, count);

        for (int i = lo; i < hi; i++)
        {
            var record = bucket.GetRecord(i);
            if (record.ModuleId != moduleId)
                continue;

            var entry = DeserializeEntry<T>(record, bucket);
            if (entry is not null)
                yield return entry;
        }
    }

    public IEnumerable<T> QueryAll<T>(Timestamp t0, Timestamp t1) where T : IEntry
    {
        if (!_buckets.TryGetValue(typeof(T), out var bucket))
            yield break;

        var count = bucket.RecordCount;
        var lo = bucket.LowerBound(t0.Nanoseconds, count);
        var hi = bucket.UpperBound(t1.Nanoseconds, count);

        for (int i = lo; i < hi; i++)
        {
            var record = bucket.GetRecord(i);
            var entry = DeserializeEntry<T>(record, bucket);
            if (entry is not null)
                yield return entry;
        }
    }

    private T? DeserializeEntry<T>(InternalRecord record, Bucket bucket) where T : IEntry
    {
        var blob = bucket.GetBlob(record);
        var entry = MemoryPackSerializer.Deserialize<T>(blob);
        if (entry is null)
            return default;

        entry.Meta = _sources.Get(record.SourceLocationId, _strings);
        return entry;
    }

    public Meta GetSourceLocation(int id)
    {
        return _sources.Get(id, _strings);
    }

    public void Dispose()
    {
        foreach (var bucket in _buckets.Values)
            bucket.Dispose();
        _sources.Dispose();
        _strings.Dispose();
    }
}
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MemoryPack;

namespace Tyr.Common.Debug.Db;

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
    private readonly FrameBucket _frames;

    // Module name → interned string id (for query filtering)
    private readonly ConcurrentDictionary<string, int> _moduleIdCache = new();

    // Single lock for interning (cold path)
    private readonly Lock _internLock = new();

    [ThreadStatic] private static ArrayBufferWriter<byte>? _serializeBuffer;
    private long _lastEntryTimestamp = long.MinValue;
    private long _lastFrameTimestamp = long.MinValue;

    public DebugDb(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);

        _strings = new MappedStringPool(Path.Combine(directory, "strings.pool"));
        _sources = new MappedSourceLocationTable(Path.Combine(directory, "sources.table"));
        _sources.Reload(_strings);
        _frames = new FrameBucket(directory);

        // Rebuild module id cache from existing storage.
        RebuildModuleCache();
    }

    private void RebuildModuleCache()
    {
        var sourceCount = _sources.Count;
        for (int i = 0; i < sourceCount; i++)
        {
            var isl = _sources.GetInternal(i);
            AddModuleToCache(isl.ModuleId);
        }

        var frameCount = _frames.RecordCount;
        for (int i = 0; i < frameCount; i++)
        {
            var frame = _frames.GetRecord(i);
            AddModuleToCache(frame.ModuleId);
        }
    }

    private void AddModuleToCache(int moduleId)
    {
        var moduleName = _strings.Get(moduleId);
        if (moduleName is not null)
            _moduleIdCache.TryAdd(moduleName, moduleId);
    }

    /// <summary>
    /// Register a type to ensure its bucket is created/loaded.
    /// Call before using Append/Query for that type.
    /// </summary>
    public DebugDb RegisterType<T>() where T : IEntry
    {
        _buckets.GetOrAdd(typeof(T), CreateBucket);
        return this;
    }

    private Bucket GetOrCreateBucket<T>() where T : IEntry
    {
        return _buckets.GetOrAdd(typeof(T), CreateBucket);
    }

    public void Append<T>(T entry) where T : IEntry
    {
        var bucket = GetOrCreateBucket<T>();
        //WarnIfTimestampDecreases(ref _lastEntryTimestamp, entry.Timestamp.Nanoseconds, $"entry append for {typeof(T).FullName}");

        int sourceId;
        int moduleId;
        lock (_internLock)
        {
            sourceId = _sources.Intern(entry.Meta, _strings);
            moduleId = _strings.Intern(entry.Meta.Module);
        }
        _moduleIdCache.TryAdd(entry.Meta.Module, moduleId);

        var buffer = _serializeBuffer ??= new ArrayBufferWriter<byte>();
        buffer.ResetWrittenCount();
        MemoryPackSerializer.Serialize(buffer, entry);

        var record = new InternalRecord
        {
            Timestamp        = entry.Timestamp.Nanoseconds,
            ModuleId         = moduleId,
            SourceLocationId = sourceId,
        };

        bucket.Append(record, buffer.WrittenSpan);
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
        try
        {
            var blob = bucket.GetBlob(record);
            var entry = MemoryPackSerializer.Deserialize<T>(blob);
            if (entry is null)
                return default;

            entry.Meta = _sources.Get(record.SourceLocationId, _strings);
            return entry;
        }
        catch (Exception ex)
        {
            Trace.WriteLine(
                $"DebugDb warning: failed to deserialize {typeof(T).FullName} at timestamp {record.Timestamp}. " +
                $"Skipping corrupt or incompatible row. {ex}");
            return default;
        }
    }

    private Bucket CreateBucket(Type type) => new(_directory, GetBucketName(type));

    private static string GetBucketName(Type type)
    {
        var typeName = type.FullName ?? type.Name;
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(typeName.Length);

        foreach (var ch in typeName)
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);

        return builder.ToString().Replace('+', '_');
    }

    public Meta GetSourceLocation(int id)
    {
        return _sources.Get(id, _strings);
    }

    public void AppendFrame(Frame frame)
    {
        //WarnIfTimestampDecreases(ref _lastFrameTimestamp, frame.StartTimestamp.Nanoseconds, $"frame append for module '{frame.ModuleName}'");

        int moduleId;
        lock (_internLock)
        {
            moduleId = _strings.Intern(frame.ModuleName);
        }
        _moduleIdCache.TryAdd(frame.ModuleName, moduleId);

        _frames.Append(new InternalFrame
        {
            Timestamp = frame.StartTimestamp.Nanoseconds,
            ModuleId  = moduleId,
        });
    }

    public IEnumerable<Frame> QueryFrames(string module, Timestamp t0, Timestamp t1)
    {
        if (!_moduleIdCache.TryGetValue(module, out var moduleId))
            yield break;

        var count = _frames.RecordCount;
        var lo = _frames.LowerBound(t0.Nanoseconds, count);
        var hi = _frames.UpperBound(t1.Nanoseconds, count);

        for (int i = lo; i < hi; i++)
        {
            var record = _frames.GetRecord(i);
            if (record.ModuleId != moduleId)
                continue;

            yield return new Frame
            {
                ModuleName     = module,
                StartTimestamp = Timestamp.FromNanoseconds(record.Timestamp),
            };
        }
    }

    public (Timestamp Start, Timestamp End)? GetFrameAt(string module, Timestamp t)
    {
        if (!_moduleIdCache.TryGetValue(module, out var moduleId))
            return null;

        var count = _frames.RecordCount;
        if (count == 0)
            return null;

        // Find the insertion point for t, then scan backwards for the frame start
        var pos = _frames.UpperBound(t.Nanoseconds, count);

        // Scan backwards from pos to find the last frame of this module with timestamp <= t
        int startIdx = -1;
        for (int i = pos - 1; i >= 0; i--)
        {
            var record = _frames.GetRecord(i);
            if (record.ModuleId == moduleId)
            {
                startIdx = i;
                break;
            }
        }

        if (startIdx < 0)
            return null;

        var start = Timestamp.FromNanoseconds(_frames.GetRecord(startIdx).Timestamp);

        // Scan forward from startIdx+1 to find the next frame of this module (= end boundary)
        for (int i = startIdx + 1; i < count; i++)
        {
            var record = _frames.GetRecord(i);
            if (record.ModuleId == moduleId)
                return (start, Timestamp.FromNanoseconds(record.Timestamp));
        }

        // Last frame of this module — open-ended, return MaxValue
        return (start, Timestamp.MaxValue);
    }

    public void Dispose()
    {
        foreach (var bucket in _buckets.Values)
            bucket.Dispose();
        _frames.Dispose();
        _sources.Dispose();
        _strings.Dispose();
    }

    [Conditional("DEBUG")]
    private static void WarnIfTimestampDecreases(ref long lastTimestamp, long timestamp, string streamName)
    {
        while (true)
        {
            var previous = Interlocked.Read(ref lastTimestamp);
            if (timestamp < previous)
            {
                Trace.WriteLine(
                    $"DebugDb warning: non-monotonic timestamp in {streamName}. Previous={previous}, Current={timestamp}. " +
                    "Range queries assume append order is timestamp-sorted.");
                return;
            }

            if (Interlocked.CompareExchange(ref lastTimestamp, timestamp, previous) == previous)
                return;
        }
    }
}

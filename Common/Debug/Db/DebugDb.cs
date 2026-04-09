using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MemoryPack;

namespace Tyr.Common.Debug.Db;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct InternalRecord
{
    public long Timestamp;
    public int BlobOffset;
    public int BlobLength;
}

public sealed class DebugDb : IDebugDb
{
    private readonly string _directory;
    private readonly MappedStringPool _strings;
    private readonly MappedSourceLocationTable _sources;
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<EntryShardKey, Bucket>> _buckets = new();
    private readonly FrameBucket _frames;

    private readonly ConcurrentDictionary<string, int> _moduleIdCache = new();
    private readonly Lock _internLock = new();

    [ThreadStatic] private static ArrayBufferWriter<byte>? _serializeBuffer;

    public DebugDb(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);

        _strings = new MappedStringPool(Path.Combine(directory, "strings.pool"));
        _sources = new MappedSourceLocationTable(Path.Combine(directory, "sources.table"));
        _sources.Reload(_strings);
        _frames = new FrameBucket(directory);

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

    public DebugDb RegisterType<T>() where T : IEntry
    {
        _buckets.GetOrAdd(typeof(T), LoadBucketsForType);
        return this;
    }

    public void Append<T>(T entry) where T : IEntry
    {
        int sourceId;
        int moduleId;
        int shardKeyId;
        lock (_internLock)
        {
            sourceId = _sources.Intern(entry.Meta, _strings);
            moduleId = _strings.Intern(entry.Meta.Module);
            shardKeyId = _strings.Intern(entry.ShardKey);
        }

        _moduleIdCache.TryAdd(entry.Meta.Module, moduleId);

        var bucket = GetOrCreateBucket(new EntryShardKey(typeof(T), moduleId, sourceId, shardKeyId));

        var buffer = _serializeBuffer ??= new ArrayBufferWriter<byte>();
        buffer.ResetWrittenCount();
        MemoryPackSerializer.Serialize(buffer, entry);

        bucket.Append(new InternalRecord
        {
            Timestamp = entry.Timestamp.Nanoseconds,
        }, buffer.WrittenSpan);
    }

    public IEnumerable<T> Query<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null) where T : IEntry
    {
        if (!_moduleIdCache.TryGetValue(module, out var moduleId))
            yield break;

        int? shardKeyId = null;
        if (shardKey is not null)
        {
            if (!_strings.TryGetId(shardKey, out var resolvedShardKeyId))
                yield break;

            shardKeyId = resolvedShardKeyId;
        }

        foreach (var entry in QueryCore<T>(t0, t1, moduleId, null, shardKeyId))
            yield return entry;
    }

    public IEnumerable<T> Query<T>(
        Timestamp t0,
        Timestamp t1,
        string? module = null,
        int? sourceLocationId = null,
        string? shardKey = null) where T : IEntry
    {
        int? moduleId = null;
        if (module is not null)
        {
            if (!_moduleIdCache.TryGetValue(module, out var resolvedModuleId))
                yield break;

            moduleId = resolvedModuleId;
        }

        int? shardKeyId = null;
        if (shardKey is not null)
        {
            if (!_strings.TryGetId(shardKey, out var resolvedShardKeyId))
                yield break;

            shardKeyId = resolvedShardKeyId;
        }

        foreach (var entry in QueryCore<T>(t0, t1, moduleId, sourceLocationId, shardKeyId))
            yield return entry;
    }

    public IEnumerable<T> QueryAll<T>(Timestamp t0, Timestamp t1, string? shardKey = null) where T : IEntry
    {
        foreach (var entry in Query<T>(t0, t1, null, null, shardKey))
            yield return entry;
    }

    private IEnumerable<T> QueryCore<T>(
        Timestamp t0,
        Timestamp t1,
        int? moduleId,
        int? sourceLocationId,
        int? shardKeyId) where T : IEntry
    {
        if (!_buckets.TryGetValue(typeof(T), out var bucketSet))
            yield break;

        var queue = new PriorityQueue<ShardCursor, long>();
        foreach (var (shard, bucket) in bucketSet)
        {
            if (moduleId.HasValue && shard.ModuleId != moduleId.Value)
                continue;

            if (sourceLocationId.HasValue && shard.SourceLocationId != sourceLocationId.Value)
                continue;

            if (shardKeyId.HasValue && shard.ShardKeyId != shardKeyId.Value)
                continue;

            var count = bucket.RecordCount;
            var lo = bucket.LowerBound(t0.Nanoseconds, count);
            var hi = bucket.UpperBound(t1.Nanoseconds, count);
            if (lo >= hi)
                continue;

            var cursor = new ShardCursor(shard, bucket, lo, hi);
            queue.Enqueue(cursor, cursor.CurrentRecord.Timestamp);
        }

        while (queue.TryDequeue(out var cursor, out _))
        {
            var record = cursor.CurrentRecord;
            var entry = DeserializeEntry<T>(record, cursor.Bucket, cursor.Shard.SourceLocationId);
            if (entry is not null)
                yield return entry;

            cursor.Index++;
            if (cursor.Index < cursor.Hi)
                queue.Enqueue(cursor, cursor.CurrentRecord.Timestamp);
        }
    }

    private T? DeserializeEntry<T>(InternalRecord record, Bucket bucket, int sourceLocationId) where T : IEntry
    {
        try
        {
            var blob = bucket.GetBlob(record);
            var entry = MemoryPackSerializer.Deserialize<T>(blob);
            if (entry is null)
                return default;

            entry.Meta = _sources.Get(sourceLocationId, _strings);
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

    private ConcurrentDictionary<EntryShardKey, Bucket> LoadBucketsForType(Type type)
    {
        var bucketSet = new ConcurrentDictionary<EntryShardKey, Bucket>();
        var typeDirectory = GetTypeDirectory(type);
        Directory.CreateDirectory(typeDirectory);

        foreach (var recordsPath in Directory.GetFiles(typeDirectory, "*.records"))
        {
            if (!TryParseShardName(type, recordsPath, out var shard))
                continue;

            bucketSet[shard] = new Bucket(typeDirectory, Path.GetFileNameWithoutExtension(recordsPath));
        }

        return bucketSet;
    }

    private Bucket GetOrCreateBucket(EntryShardKey shard)
    {
        var bucketSet = _buckets.GetOrAdd(shard.Type, LoadBucketsForType);
        return bucketSet.GetOrAdd(shard, key =>
        {
            var typeDirectory = GetTypeDirectory(key.Type);
            Directory.CreateDirectory(typeDirectory);
            return new Bucket(typeDirectory, GetShardName(key));
        });
    }

    private string GetTypeDirectory(Type type) => Path.Combine(_directory, GetBucketName(type));

    private static string GetShardName(EntryShardKey shard) =>
        $"m{shard.ModuleId}_s{shard.SourceLocationId}_k{shard.ShardKeyId}";

    private static bool TryParseShardName(Type type, string recordsPath, out EntryShardKey shard)
    {
        var name = Path.GetFileNameWithoutExtension(recordsPath);
        var parts = name.Split('_');
        if (parts.Length != 3 ||
            !TryParsePart(parts[0], 'm', out var moduleId) ||
            !TryParsePart(parts[1], 's', out var sourceLocationId) ||
            !TryParsePart(parts[2], 'k', out var shardKeyId))
        {
            shard = default;
            return false;
        }

        shard = new EntryShardKey(type, moduleId, sourceLocationId, shardKeyId);
        return true;

        static bool TryParsePart(string part, char prefix, out int value)
        {
            if (part.Length < 2 || part[0] != prefix)
            {
                value = default;
                return false;
            }

            return int.TryParse(part[1..], out value);
        }
    }

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
        int moduleId;
        lock (_internLock)
        {
            moduleId = _strings.Intern(frame.ModuleName);
        }
        _moduleIdCache.TryAdd(frame.ModuleName, moduleId);

        _frames.Append(new InternalFrame
        {
            Timestamp = frame.StartTimestamp.Nanoseconds,
            ModuleId = moduleId,
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
                ModuleName = module,
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

        var pos = _frames.UpperBound(t.Nanoseconds, count);

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

        for (int i = startIdx + 1; i < count; i++)
        {
            var record = _frames.GetRecord(i);
            if (record.ModuleId == moduleId)
                return (start, Timestamp.FromNanoseconds(record.Timestamp));
        }

        return (start, Timestamp.MaxValue);
    }

    public void Dispose()
    {
        foreach (var bucketSet in _buckets.Values)
        {
            foreach (var bucket in bucketSet.Values)
                bucket.Dispose();
        }

        _frames.Dispose();
        _sources.Dispose();
        _strings.Dispose();
    }

    private sealed class ShardCursor
    {
        public EntryShardKey Shard { get; }
        public Bucket Bucket { get; }
        public int Index { get; set; }
        public int Hi { get; }

        public InternalRecord CurrentRecord => Bucket.GetRecord(Index);

        public ShardCursor(EntryShardKey shard, Bucket bucket, int index, int hi)
        {
            Shard = shard;
            Bucket = bucket;
            Index = index;
            Hi = hi;
        }
    }
}

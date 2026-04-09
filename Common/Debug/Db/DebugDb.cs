using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
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
    private readonly ConcurrentDictionary<Type, Lazy<BucketSet>> _buckets = new();
    private readonly FrameBucket _frames;

    private readonly ConcurrentDictionary<string, int> _moduleIdCache = new();
    private readonly ConcurrentDictionary<string, int> _shardKeyIdCache = new();
    private readonly ConcurrentDictionary<Meta, int> _sourceIdCache = new();
    private readonly ConcurrentDictionary<int, Meta> _sourceLocationCache = new();
    private readonly ConcurrentDictionary<int, ModuleFrameIndex> _frameIndices = new();
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
            var source = _sources.Get(i, _strings);
            _sourceIdCache.TryAdd(source, i);
            _sourceLocationCache.TryAdd(i, source);
        }

        var frameCount = _frames.RecordCount;
        for (int i = 0; i < frameCount; i++)
        {
            var frame = _frames.GetRecord(i);
            AddModuleToCache(frame.ModuleId);
            GetOrCreateFrameIndex(frame.ModuleId).Add(frame.Timestamp);
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
        GetOrCreateBucketSet(typeof(T));
        return this;
    }

    public void Append<T>(T entry) where T : IEntry
    {
        var meta = entry.Meta;
        var module = meta.Module;
        var shardKey = entry.ShardKey;

        if (!_moduleIdCache.TryGetValue(module, out var moduleId) ||
            !_sourceIdCache.TryGetValue(meta, out var sourceId) ||
            !TryGetShardKeyId(shardKey, out var shardKeyId))
        {
            lock (_internLock)
            {
                if (!_moduleIdCache.TryGetValue(module, out moduleId))
                {
                    moduleId = _strings.Intern(module);
                    _moduleIdCache.TryAdd(module, moduleId);
                }

                if (!_sourceIdCache.TryGetValue(meta, out sourceId))
                {
                    sourceId = _sources.Intern(meta, _strings);
                    _sourceIdCache.TryAdd(meta, sourceId);
                    _sourceLocationCache.TryAdd(sourceId, meta);
                }

                if (!TryGetShardKeyId(shardKey, out shardKeyId))
                {
                    shardKeyId = _strings.Intern(shardKey);
                    if (shardKey is not null)
                        _shardKeyIdCache.TryAdd(shardKey, shardKeyId);
                }
            }
        }

        var bucket = GetOrCreateBucket(new EntryShardKey(typeof(T), moduleId, sourceId, shardKeyId));

        var buffer = _serializeBuffer ??= new ArrayBufferWriter<byte>();
        buffer.ResetWrittenCount();
        MemoryPackSerializer.Serialize(buffer, entry);

        bucket.Append(new InternalRecord
        {
            Timestamp = entry.Timestamp.Nanoseconds,
        }, buffer.WrittenSpan);
    }

    public IEnumerable<T> Query<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : IEntry
    {
        if (!_moduleIdCache.TryGetValue(module, out var moduleId))
            yield break;

        int? shardKeyId = null;
        if (shardKey is not null)
        {
            if (!TryGetShardKeyId(shardKey, out var resolvedShardKeyId))
                yield break;

            shardKeyId = resolvedShardKeyId;
        }

        foreach (var entry in QueryCore<T>(t0, t1, moduleId, null, shardKeyId, maxCount, hydrateMeta: true))
            yield return entry;
    }

    public IEnumerable<T> QueryWithoutMeta<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : IEntry
    {
        if (!_moduleIdCache.TryGetValue(module, out var moduleId))
            yield break;

        int? shardKeyId = null;
        if (shardKey is not null)
        {
            if (!TryGetShardKeyId(shardKey, out var resolvedShardKeyId))
                yield break;

            shardKeyId = resolvedShardKeyId;
        }

        foreach (var entry in QueryCore<T>(t0, t1, moduleId, null, shardKeyId, maxCount, hydrateMeta: false))
            yield return entry;
    }

    public IEnumerable<T> Query<T>(
        Timestamp t0,
        Timestamp t1,
        string? module = null,
        int? sourceLocationId = null,
        string? shardKey = null,
        int? maxCount = null) where T : IEntry
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
            if (!TryGetShardKeyId(shardKey, out var resolvedShardKeyId))
                yield break;

            shardKeyId = resolvedShardKeyId;
        }

        foreach (var entry in QueryCore<T>(t0, t1, moduleId, sourceLocationId, shardKeyId, maxCount, hydrateMeta: true))
            yield return entry;
    }

    public IEnumerable<T> QueryAll<T>(Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : IEntry
    {
        foreach (var entry in Query<T>(t0, t1, null, null, shardKey, maxCount))
            yield return entry;
    }

    public IEnumerable<string> QueryModules()
    {
        foreach (var module in _moduleIdCache.Keys.Order())
            yield return module;
    }

    public IEnumerable<string> QueryShardKeys<T>(string module) where T : IEntry
    {
        if (!_moduleIdCache.TryGetValue(module, out var moduleId))
            yield break;

        if (!TryGetBucketSet(typeof(T), out var bucketSet))
            yield break;

        var shardKeys = bucketSet.GetByModule(moduleId).Keys
            .Where(shard => shard.ShardKeyId >= 0)
            .Select(shard => shard.ShardKeyId)
            .Distinct()
            .Select(_strings.Get)
            .OfType<string>()
            .Order();

        foreach (var shardKey in shardKeys)
            yield return shardKey;
    }

    public IEnumerable<Meta> QuerySourceLocations<T>(string module) where T : IEntry
    {
        if (!_moduleIdCache.TryGetValue(module, out var moduleId))
            yield break;

        if (!TryGetBucketSet(typeof(T), out var bucketSet))
            yield break;

        var sourceLocationIds = bucketSet.GetByModule(moduleId).Keys
            .Select(shard => shard.SourceLocationId)
            .Distinct()
            .Order();

        foreach (var sourceLocationId in sourceLocationIds)
            yield return _sourceLocationCache.GetOrAdd(sourceLocationId, id => _sources.Get(id, _strings));
    }

    public Meta? TryGetShardMeta<T>(string module, string shardKey) where T : IEntry
    {
        if (!_moduleIdCache.TryGetValue(module, out var moduleId))
            return null;

        if (!TryGetShardKeyId(shardKey, out var shardKeyId))
            return null;

        if (!TryGetBucketSet(typeof(T), out var bucketSet))
            return null;

        foreach (var (shard, _) in bucketSet.GetCandidates(moduleId, null, shardKeyId))
            return _sourceLocationCache.GetOrAdd(shard.SourceLocationId, id => _sources.Get(id, _strings));

        return null;
    }

    private IEnumerable<T> QueryCore<T>(
        Timestamp t0,
        Timestamp t1,
        int? moduleId,
        int? sourceLocationId,
        int? shardKeyId,
        int? maxCount,
        bool hydrateMeta) where T : IEntry
    {
        if (!TryGetBucketSet(typeof(T), out var bucketSet))
            yield break;

        if (maxCount <= 0)
            yield break;

        var matches = new List<ShardMatch>();
        var totalCount = 0;
        foreach (var (shard, bucketFactory) in bucketSet.GetCandidates(moduleId, sourceLocationId, shardKeyId))
        {
            var bucket = bucketFactory.Value;
            var count = bucket.RecordCount;
            var lo = bucket.LowerBound(t0.Nanoseconds, count);
            var hi = bucket.UpperBound(t1.Nanoseconds, count);
            if (lo >= hi)
                continue;

            matches.Add(new ShardMatch(shard, bucket, lo, hi));
            totalCount += hi - lo;
        }

        if (matches.Count == 0)
            yield break;

        if (matches.Count == 1)
        {
            if (!maxCount.HasValue || totalCount <= maxCount.Value)
            {
                foreach (var entry in EnumerateSingleShardRange<T>(matches[0], hydrateMeta))
                    yield return entry;

                yield break;
            }

            foreach (var entry in EnumerateSampledSingleShard<T>(matches[0], totalCount, maxCount.Value, hydrateMeta))
                yield return entry;

            yield break;
        }

        if (!maxCount.HasValue || totalCount <= maxCount.Value)
        {
            foreach (var entry in EnumerateAllMatches<T>(matches, hydrateMeta))
                yield return entry;

            yield break;
        }

        foreach (var entry in EnumerateSampledMatches<T>(matches, totalCount, maxCount.Value, hydrateMeta))
            yield return entry;
    }

    private IEnumerable<T> EnumerateSingleShardRange<T>(ShardMatch match, bool hydrateMeta) where T : IEntry
    {
        for (var index = match.Lo; index < match.Hi; index++)
        {
            var record = match.Bucket.GetRecord(index);
            var entry = DeserializeEntry<T>(record, match.Bucket, match.Shard.SourceLocationId, hydrateMeta);
            if (entry is not null)
                yield return entry;
        }
    }

    private IEnumerable<T> EnumerateAllMatches<T>(List<ShardMatch> matches, bool hydrateMeta) where T : IEntry
    {
        var queue = new PriorityQueue<ShardCursor, long>();
        foreach (var match in matches)
        {
            var cursor = new ShardCursor(match.Shard, match.Bucket, match.Lo, match.Hi);
            queue.Enqueue(cursor, cursor.CurrentRecord.Timestamp);
        }

        while (queue.TryDequeue(out var cursor, out _))
        {
            var record = cursor.CurrentRecord;
            var entry = DeserializeEntry<T>(record, cursor.Bucket, cursor.Shard.SourceLocationId, hydrateMeta);
            if (entry is not null)
                yield return entry;

            cursor.Index++;
            if (cursor.Index < cursor.Hi)
                queue.Enqueue(cursor, cursor.CurrentRecord.Timestamp);
        }
    }

    private IEnumerable<T> EnumerateSampledSingleShard<T>(ShardMatch match, int totalCount, int maxCount, bool hydrateMeta) where T : IEntry
    {
        var sampleCount = System.Math.Min(totalCount, maxCount);
        if (sampleCount <= 0)
            yield break;

        if (sampleCount == 1)
        {
            var record = match.Bucket.GetRecord(match.Lo);
            var entry = DeserializeEntry<T>(record, match.Bucket, match.Shard.SourceLocationId, hydrateMeta);
            if (entry is not null)
                yield return entry;
            yield break;
        }

        for (int i = 0; i < sampleCount; i++)
        {
            var relativeIndex = (int)((long)i * (totalCount - 1) / (sampleCount - 1));
            var absoluteIndex = match.Lo + relativeIndex;
            var record = match.Bucket.GetRecord(absoluteIndex);
            var entry = DeserializeEntry<T>(record, match.Bucket, match.Shard.SourceLocationId, hydrateMeta);
            if (entry is not null)
                yield return entry;
        }
    }

    private IEnumerable<T> EnumerateSampledMatches<T>(List<ShardMatch> matches, int totalCount, int maxCount, bool hydrateMeta) where T : IEntry
    {
        var targetIndices = GetSampledIndices(totalCount, maxCount);
        var targetPosition = 0;
        var logicalIndex = 0;

        var queue = new PriorityQueue<ShardCursor, long>();
        foreach (var match in matches)
        {
            var cursor = new ShardCursor(match.Shard, match.Bucket, match.Lo, match.Hi);
            queue.Enqueue(cursor, cursor.CurrentRecord.Timestamp);
        }

        while (queue.TryDequeue(out var cursor, out _))
        {
            if (logicalIndex == targetIndices[targetPosition])
            {
                var record = cursor.CurrentRecord;
                var entry = DeserializeEntry<T>(record, cursor.Bucket, cursor.Shard.SourceLocationId, hydrateMeta);
                if (entry is not null)
                    yield return entry;

                targetPosition++;
                if (targetPosition >= targetIndices.Count)
                    yield break;
            }

            logicalIndex++;
            cursor.Index++;
            if (cursor.Index < cursor.Hi)
                queue.Enqueue(cursor, cursor.CurrentRecord.Timestamp);
        }
    }

    private static List<int> GetSampledIndices(int totalCount, int maxCount)
    {
        var sampleCount = System.Math.Min(totalCount, maxCount);
        var indices = new List<int>(sampleCount);

        if (sampleCount == 1)
        {
            indices.Add(0);
            return indices;
        }

        for (int i = 0; i < sampleCount; i++)
        {
            indices.Add((int)((long)i * (totalCount - 1) / (sampleCount - 1)));
        }

        return indices;
    }

    private bool TryGetShardKeyId(string? shardKey, out int shardKeyId)
    {
        if (shardKey is null)
        {
            shardKeyId = -1;
            return true;
        }

        return _shardKeyIdCache.TryGetValue(shardKey, out shardKeyId) ||
               _strings.TryGetId(shardKey, out shardKeyId);
    }

    private T? DeserializeEntry<T>(InternalRecord record, Bucket bucket, int sourceLocationId, bool hydrateMeta) where T : IEntry
    {
        try
        {
            var blob = bucket.GetBlob(record);
            var entry = MemoryPackSerializer.Deserialize<T>(blob);
            if (entry is null)
                return default;

            entry.Timestamp = Timestamp.FromNanoseconds(record.Timestamp);
            entry.Meta = hydrateMeta
                ? _sourceLocationCache.GetOrAdd(sourceLocationId, id => _sources.Get(id, _strings))
                : Meta.Empty;
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

    private BucketSet LoadBucketsForType(Type type)
    {
        var bucketSet = new BucketSet();
        var typeDirectory = GetTypeDirectory(type);
        Directory.CreateDirectory(typeDirectory);

        foreach (var recordsPath in Directory.GetFiles(typeDirectory, "*.records"))
        {
            if (!TryParseShardName(type, recordsPath, out var shard))
                continue;

            if (shard.ShardKeyId >= 0)
            {
                var shardKey = _strings.Get(shard.ShardKeyId);
                if (shardKey is not null)
                    _shardKeyIdCache.TryAdd(shardKey, shard.ShardKeyId);
            }

            bucketSet.Add(shard, CreateBucketFactory(typeDirectory, Path.GetFileNameWithoutExtension(recordsPath)));
        }

        return bucketSet;
    }

    private Bucket GetOrCreateBucket(EntryShardKey shard)
    {
        var bucketSet = GetOrCreateBucketSet(shard.Type);
        return bucketSet.GetOrAdd(shard, key =>
        {
            var typeDirectory = GetTypeDirectory(key.Type);
            Directory.CreateDirectory(typeDirectory);
            return CreateBucketFactory(typeDirectory, GetShardName(key));
        }).Value;
    }

    private BucketSet GetOrCreateBucketSet(Type type)
    {
        return _buckets.GetOrAdd(
            type,
            static (bucketOwner, state) => new Lazy<BucketSet>(
                () => state.LoadBucketsForType(bucketOwner),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this).Value;
    }

    private bool TryGetBucketSet(Type type, out BucketSet bucketSet)
    {
        if (_buckets.TryGetValue(type, out var bucketSetFactory))
        {
            bucketSet = bucketSetFactory.Value;
            return true;
        }

        bucketSet = null!;
        return false;
    }

    private static Lazy<Bucket> CreateBucketFactory(string typeDirectory, string shardName)
    {
        return new Lazy<Bucket>(
            () => new Bucket(typeDirectory, shardName),
            LazyThreadSafetyMode.ExecutionAndPublication);
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

        GetOrCreateFrameIndex(moduleId).Add(frame.StartTimestamp.Nanoseconds);
    }

    public (Timestamp Start, Timestamp End)? GetFrameRange()
    {
        var count = _frames.RecordCount;
        if (count == 0)
            return null;

        var start = Timestamp.FromNanoseconds(_frames.GetRecord(0).Timestamp);
        var end = Timestamp.FromNanoseconds(_frames.GetRecord(count - 1).Timestamp);
        return (start, end);
    }

    public IEnumerable<Frame> QueryFrames(string module, Timestamp t0, Timestamp t1)
    {
        if (!_moduleIdCache.TryGetValue(module, out var moduleId))
            yield break;

        if (!_frameIndices.TryGetValue(moduleId, out var frameIndex))
            yield break;

        foreach (var timestamp in frameIndex.GetRange(t0.Nanoseconds, t1.Nanoseconds))
        {
            yield return new Frame
            {
                ModuleName = module,
                StartTimestamp = Timestamp.FromNanoseconds(timestamp),
            };
        }
    }

    public (Timestamp Start, Timestamp End)? GetFrameAt(string module, Timestamp t)
    {
        if (!_moduleIdCache.TryGetValue(module, out var moduleId))
            return null;

        if (!_frameIndices.TryGetValue(moduleId, out var frameIndex))
            return null;

        if (!frameIndex.TryGetFrameAt(t.Nanoseconds, out var start, out var end))
            return null;

        return (
            Timestamp.FromNanoseconds(start),
            end.HasValue ? Timestamp.FromNanoseconds(end.Value) : Timestamp.MaxValue);
    }

    private ModuleFrameIndex GetOrCreateFrameIndex(int moduleId)
    {
        return _frameIndices.GetOrAdd(moduleId, static _ => new ModuleFrameIndex());
    }

    public void Dispose()
    {
        foreach (var bucketSetFactory in _buckets.Values)
        {
            if (!bucketSetFactory.IsValueCreated)
                continue;

            foreach (var bucketFactory in bucketSetFactory.Value.All.Values)
            {
                if (bucketFactory.IsValueCreated)
                    bucketFactory.Value.Dispose();
            }
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

    private sealed class ShardMatch
    {
        public EntryShardKey Shard { get; }
        public Bucket Bucket { get; }
        public int Lo { get; }
        public int Hi { get; }

        public ShardMatch(EntryShardKey shard, Bucket bucket, int lo, int hi)
        {
            Shard = shard;
            Bucket = bucket;
            Lo = lo;
            Hi = hi;
        }
    }

    private sealed class ModuleFrameIndex
    {
        private readonly Lock _lock = new();
        private readonly List<long> _timestamps = [];

        public void Add(long timestamp)
        {
            lock (_lock)
                _timestamps.Add(timestamp);
        }

        public IEnumerable<long> GetRange(long t0, long t1)
        {
            long[] timestamps;
            lock (_lock)
                timestamps = _timestamps.ToArray();

            var lo = LowerBound(timestamps, t0);
            var hi = UpperBound(timestamps, t1);
            for (var i = lo; i < hi; i++)
                yield return timestamps[i];
        }

        public bool TryGetFrameAt(long timestamp, out long start, out long? end)
        {
            lock (_lock)
            {
                var pos = UpperBound(_timestamps, timestamp);
                if (pos == 0)
                {
                    start = default;
                    end = default;
                    return false;
                }

                start = _timestamps[pos - 1];
                end = pos < _timestamps.Count ? _timestamps[pos] : null;
                return true;
            }
        }

        private static int LowerBound(IReadOnlyList<long> timestamps, long value)
        {
            var lo = 0;
            var hi = timestamps.Count;
            while (lo < hi)
            {
                var mid = lo + (hi - lo) / 2;
                if (timestamps[mid] < value) lo = mid + 1;
                else hi = mid;
            }

            return lo;
        }

        private static int UpperBound(IReadOnlyList<long> timestamps, long value)
        {
            var lo = 0;
            var hi = timestamps.Count;
            while (lo < hi)
            {
                var mid = lo + (hi - lo) / 2;
                if (timestamps[mid] <= value) lo = mid + 1;
                else hi = mid;
            }

            return lo;
        }
    }

    private readonly record struct ModuleShardKey(int ModuleId, int ShardKeyId);

    private sealed class BucketSet
    {
        public ConcurrentDictionary<EntryShardKey, Lazy<Bucket>> All { get; } = new();

        private readonly ConcurrentDictionary<int, ConcurrentDictionary<EntryShardKey, Lazy<Bucket>>> _byModule = new();
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<EntryShardKey, Lazy<Bucket>>> _bySourceLocation = new();
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<EntryShardKey, Lazy<Bucket>>> _byShardKey = new();
        private readonly ConcurrentDictionary<ModuleShardKey, ConcurrentDictionary<EntryShardKey, Lazy<Bucket>>> _byModuleAndShardKey = new();

        public Lazy<Bucket> GetOrAdd(EntryShardKey shard, Func<EntryShardKey, Lazy<Bucket>> valueFactory)
        {
            return All.GetOrAdd(shard, key =>
            {
                var bucketFactory = valueFactory(key);
                Index(key, bucketFactory);
                return bucketFactory;
            });
        }

        public void Add(EntryShardKey shard, Lazy<Bucket> bucketFactory)
        {
            if (All.TryAdd(shard, bucketFactory))
                Index(shard, bucketFactory);
        }

        public ConcurrentDictionary<EntryShardKey, Lazy<Bucket>> GetByModule(int moduleId)
        {
            return _byModule.TryGetValue(moduleId, out var buckets)
                ? buckets
                : Empty;
        }

        public IEnumerable<KeyValuePair<EntryShardKey, Lazy<Bucket>>> GetCandidates(
            int? moduleId,
            int? sourceLocationId,
            int? shardKeyId)
        {
            if (sourceLocationId.HasValue)
            {
                var sourceBuckets = GetBySourceLocation(sourceLocationId.Value);

                foreach (var entry in sourceBuckets)
                {
                    if (moduleId.HasValue && entry.Key.ModuleId != moduleId.Value)
                        continue;

                    if (shardKeyId.HasValue && entry.Key.ShardKeyId != shardKeyId.Value)
                        continue;

                    yield return entry;
                }

                yield break;
            }

            if (moduleId.HasValue && shardKeyId.HasValue)
            {
                foreach (var entry in GetByModuleAndShardKey(moduleId.Value, shardKeyId.Value))
                    yield return entry;

                yield break;
            }

            if (moduleId.HasValue)
            {
                foreach (var entry in GetByModule(moduleId.Value))
                    yield return entry;

                yield break;
            }

            if (shardKeyId.HasValue)
            {
                foreach (var entry in GetByShardKey(shardKeyId.Value))
                    yield return entry;

                yield break;
            }

            foreach (var entry in All)
                yield return entry;
        }

        private ConcurrentDictionary<EntryShardKey, Lazy<Bucket>> GetBySourceLocation(int sourceLocationId)
        {
            return _bySourceLocation.TryGetValue(sourceLocationId, out var buckets)
                ? buckets
                : Empty;
        }

        private ConcurrentDictionary<EntryShardKey, Lazy<Bucket>> GetByShardKey(int shardKeyId)
        {
            return _byShardKey.TryGetValue(shardKeyId, out var buckets)
                ? buckets
                : Empty;
        }

        private ConcurrentDictionary<EntryShardKey, Lazy<Bucket>> GetByModuleAndShardKey(int moduleId, int shardKeyId)
        {
            return _byModuleAndShardKey.TryGetValue(new ModuleShardKey(moduleId, shardKeyId), out var buckets)
                ? buckets
                : Empty;
        }

        private void Index(EntryShardKey shard, Lazy<Bucket> bucketFactory)
        {
            _byModule.GetOrAdd(shard.ModuleId, static _ => new ConcurrentDictionary<EntryShardKey, Lazy<Bucket>>())[shard] = bucketFactory;
            _bySourceLocation.GetOrAdd(shard.SourceLocationId, static _ => new ConcurrentDictionary<EntryShardKey, Lazy<Bucket>>())[shard] = bucketFactory;
            _byShardKey.GetOrAdd(shard.ShardKeyId, static _ => new ConcurrentDictionary<EntryShardKey, Lazy<Bucket>>())[shard] = bucketFactory;
            _byModuleAndShardKey.GetOrAdd(
                new ModuleShardKey(shard.ModuleId, shard.ShardKeyId),
                static _ => new ConcurrentDictionary<EntryShardKey, Lazy<Bucket>>())[shard] = bucketFactory;
        }

        private static readonly ConcurrentDictionary<EntryShardKey, Lazy<Bucket>> Empty = new();
    }
}

using System.Buffers;
using System.Collections.Concurrent;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Tyr.Common.Debug.Logging;
using ZLogger;

namespace Tyr.Common.Debug.Db;

/// <summary>
/// Append-only, memory-mapped store of debug entries, sharded per
/// (entry type, module, source location, shard key). Records within a shard are
/// timestamp-ordered, so time-range queries are two binary searches per shard and
/// a k-way merge across shards.
///
/// Threading: one appending thread (the dumper); any number of query threads.
/// Everything a query touches is either immutable, a copy-on-write snapshot, or
/// a mapping view that stays valid until the database is disposed.
/// </summary>
public sealed class DebugDb : IDebugDb
{
    private readonly string _directory;
    private readonly MappedStringPool _strings;
    private readonly MappedSourceLocationTable _sources;
    private readonly FrameBucket _frames;
    private readonly ConcurrentDictionary<Type, Lazy<BucketSet>> _buckets = new();

    // Module name → string id. Also the set of modules known to this database.
    private readonly ConcurrentDictionary<string, int> _moduleIds = new(StringComparer.Ordinal);

    // Meta.Id → source location id, -1 when not interned yet. Written under _internLock,
    // read lock-free on the append fast path; the array is replaced (never shrunk) on growth.
    private int[] _sourceIdByMeta = CreateSourceIdTable(256);

    // Source location id → resolved Meta / module id. Filled by the interning path and
    // lazily by queries; Meta instances are interned so a racing double-resolve is benign.
    // Both grow together under _internLock and are published before _sourceIdByMeta.
    private Meta?[] _metaBySourceId = new Meta?[256];
    private int[] _moduleIdBySourceId = new int[256];

    private readonly ConcurrentDictionary<int, ModuleFrameIndex> _frameIndices = new();
    private readonly Lock _internLock = new();

    private readonly List<JournalGroup> _journal = [];
    private readonly Dictionary<(Meta Meta, string Message, LogLevel Level), int> _journalIndex = new();
    private readonly Lock _journalLock = new();
    private const LogLevel JournalMinLevel = LogLevel.Information;
    private bool _groupJournalEntries = true;

    [ThreadStatic] private static ArrayBufferWriter<byte>? _serializeBuffer;

    public DebugDb(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);

        _strings = new MappedStringPool(Path.Combine(directory, "strings.pool"));
        _sources = new MappedSourceLocationTable(Path.Combine(directory, "sources.table"));
        _frames = new FrameBucket(directory);

        LoadPersistedState();
    }

    private void LoadPersistedState()
    {
        var sourceCount = _sources.Count;
        for (var i = 0; i < sourceCount; i++)
            ResolveMeta(i);

        var frames = _frames.View;
        var frameCount = frames.RecordCount;
        for (var i = 0; i < frameCount; i++)
        {
            var frame = frames.GetRecord(i);
            AddModule(frame.ModuleId);
            GetOrCreateFrameIndex(frame.ModuleId).Add(frame.Timestamp);
        }
    }

    private void AddModule(int moduleId)
    {
        var moduleName = _strings.Get(moduleId);
        if (moduleName is not null)
            _moduleIds.TryAdd(moduleName, moduleId);
    }

    private static int[] CreateSourceIdTable(int size)
    {
        var table = new int[size];
        Array.Fill(table, -1);
        return table;
    }

    // ─── Registration ───────────────────────────────────────────────────────

    public DebugDb RegisterType<T>() where T : struct, IEntry
    {
        GetOrCreateBucketSet(typeof(T));
        return this;
    }

    public DebugDb RegisterType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!type.IsValueType || !typeof(IEntry).IsAssignableFrom(type))
            throw new ArgumentException($"Debug entry type {type.FullName} must be a value type implementing {nameof(IEntry)}.", nameof(type));

        GetOrCreateBucketSet(type);
        return this;
    }

    // ─── Append ─────────────────────────────────────────────────────────────

    public void Append<T>(T entry) where T : struct, IEntry
    {
        // A default-constructed entry has no Meta; normalize once so the shard, the
        // journal, and anything reading the entry back all see the same thing.
        entry.Meta ??= Meta.Empty;

        var sourceId = GetSourceId(entry.Meta);
        var moduleId = Volatile.Read(ref _moduleIdBySourceId)[sourceId];
        var shardKeyId = GetShardKeyId(entry.ShardKey);

        var shard = GetOrCreateBucketSet(typeof(T))
            .GetOrAdd(new EntryShardKey(typeof(T), moduleId, sourceId, shardKeyId));

        var buffer = _serializeBuffer ??= new ArrayBufferWriter<byte>();
        buffer.ResetWrittenCount();
        MemoryPackSerializer.Serialize(buffer, entry);

        shard.Bucket.Append(entry.Timestamp.Nanoseconds, buffer.WrittenSpan);

        if (entry is Entry logEntry && logEntry.Level >= JournalMinLevel)
        {
            lock (_journalLock)
                AddToJournal(logEntry);
        }
    }

    private int GetSourceId(Meta meta)
    {
        var table = Volatile.Read(ref _sourceIdByMeta);
        if ((uint)meta.Id < (uint)table.Length)
        {
            var sourceId = table[meta.Id];
            if (sourceId >= 0)
                return sourceId;
        }

        return InternSource(meta);
    }

    private int InternSource(Meta meta)
    {
        lock (_internLock)
        {
            var table = _sourceIdByMeta;
            if (meta.Id < table.Length && table[meta.Id] >= 0)
                return table[meta.Id];

            var sourceId = _sources.Intern(meta, _strings);
            var moduleId = _sources.GetInternal(sourceId).ModuleId;
            StoreSource(sourceId, meta, moduleId);

            if (meta.Id >= table.Length)
            {
                var grown = CreateSourceIdTable(System.Math.Max(table.Length * 2, meta.Id + 1));
                Array.Copy(table, grown, table.Length);
                table = grown;
            }

            table[meta.Id] = sourceId;
            Volatile.Write(ref _sourceIdByMeta, table);
            return sourceId;
        }
    }

    // Must be called under _internLock. Publishes the per-source tables before returning,
    // so a reader that later finds sourceId through _sourceIdByMeta sees both entries.
    private void StoreSource(int sourceId, Meta meta, int moduleId)
    {
        var metas = _metaBySourceId;
        var modules = _moduleIdBySourceId;
        if (sourceId >= metas.Length)
        {
            var size = System.Math.Max(metas.Length * 2, sourceId + 1);
            var grownMetas = new Meta?[size];
            var grownModules = new int[size];
            Array.Copy(metas, grownMetas, metas.Length);
            Array.Copy(modules, grownModules, modules.Length);
            metas = grownMetas;
            modules = grownModules;
        }

        modules[sourceId] = moduleId;
        metas[sourceId] = meta;
        Volatile.Write(ref _moduleIdBySourceId, modules);
        Volatile.Write(ref _metaBySourceId, metas);
        AddModule(moduleId);
    }

    private int GetShardKeyId(string? shardKey)
    {
        if (shardKey is null)
            return -1;

        if (_strings.TryGetId(shardKey, out var id))
            return id;

        lock (_internLock)
            return _strings.Intern(shardKey);
    }

    /// <summary>Meta for a persisted source location id, resolving and caching it on first use.</summary>
    private Meta ResolveMeta(int sourceId)
    {
        var metas = Volatile.Read(ref _metaBySourceId);
        if ((uint)sourceId < (uint)metas.Length && metas[sourceId] is { } cached)
            return cached;

        var meta = _sources.Get(sourceId, _strings);
        var moduleId = _sources.GetInternal(sourceId).ModuleId;

        lock (_internLock)
        {
            StoreSource(sourceId, meta, moduleId);

            var table = _sourceIdByMeta;
            if (meta.Id >= table.Length)
            {
                var grown = CreateSourceIdTable(System.Math.Max(table.Length * 2, meta.Id + 1));
                Array.Copy(table, grown, table.Length);
                table = grown;
            }

            if (table[meta.Id] < 0)
                table[meta.Id] = sourceId;

            Volatile.Write(ref _sourceIdByMeta, table);
        }

        return meta;
    }

    // Must be called under _journalLock.
    private void AddToJournal(Entry entry)
    {
        if (_groupJournalEntries)
        {
            var key = (entry.Meta, entry.Message, entry.Level);
            if (_journalIndex.TryGetValue(key, out var idx))
            {
                var g = _journal[idx];
                _journal[idx] = g with { Count = g.Count + 1, LastTime = entry.Timestamp };
                return;
            }

            _journalIndex[key] = _journal.Count;
        }

        _journal.Add(new JournalGroup(entry, 1, entry.Timestamp));
    }

    // ─── Queries ────────────────────────────────────────────────────────────

    public int QueryInto<T>(List<T> destination, string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null, Func<Meta, bool>? metaFilter = null) where T : struct, IEntry
    {
        return QueryInto(destination, t0, t1, module, null, shardKey, maxCount, metaFilter);
    }

    public int QueryInto<T>(List<T> destination, Timestamp t0, Timestamp t1, string? module = null, int? sourceLocationId = null, string? shardKey = null, int? maxCount = null, Func<Meta, bool>? metaFilter = null) where T : struct, IEntry
    {
        ArgumentNullException.ThrowIfNull(destination);

        int? moduleId = null;
        if (module is not null)
        {
            if (!_moduleIds.TryGetValue(module, out var resolvedModuleId))
                return 0;

            moduleId = resolvedModuleId;
        }

        int? shardKeyId = null;
        if (shardKey is not null)
        {
            if (!_strings.TryGetId(shardKey, out var resolvedShardKeyId))
                return 0;

            shardKeyId = resolvedShardKeyId;
        }

        return QueryCore<T, ListSink<T>>(new ListSink<T>(destination), t0.Nanoseconds, t1.Nanoseconds, moduleId, sourceLocationId, shardKeyId, maxCount, metaFilter);
    }

    public IEnumerable<T> Query<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
    {
        var results = new List<T>();
        QueryInto(results, t0, t1, module, null, shardKey, maxCount);
        return results;
    }

    public IEnumerable<T> Query<T>(Timestamp t0, Timestamp t1, string? module = null, int? sourceLocationId = null, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
    {
        var results = new List<T>();
        QueryInto(results, t0, t1, module, sourceLocationId, shardKey, maxCount);
        return results;
    }

    public IEnumerable<T> QueryAll<T>(Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
    {
        return Query<T>(t0, t1, null, null, shardKey, maxCount);
    }

    public IEnumerable<string> QueryModules()
    {
        return _moduleIds.Keys.Order();
    }

    public IEnumerable<string> QueryShardKeys<T>(string module) where T : struct, IEntry
    {
        if (!_moduleIds.TryGetValue(module, out var moduleId) || !TryGetBucketSet(typeof(T), out var bucketSet))
            return [];

        return bucketSet.GetModuleView(moduleId).ShardKeys;
    }

    public IEnumerable<Meta> QuerySourceLocations<T>(string module) where T : struct, IEntry
    {
        return QuerySourceLocations(module, typeof(T));
    }

    public IEnumerable<Meta> QuerySourceLocations(string module, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!_moduleIds.TryGetValue(module, out var moduleId) || !TryGetBucketSet(type, out var bucketSet))
            return [];

        var ids = bucketSet.GetModuleView(moduleId).SourceLocationIds;
        var metas = new Meta[ids.Length];
        for (var i = 0; i < ids.Length; i++)
            metas[i] = ResolveMeta(ids[i]);

        return metas;
    }

    public Meta? TryGetShardMeta<T>(string module, string shardKey) where T : struct, IEntry
    {
        if (!_moduleIds.TryGetValue(module, out var moduleId) ||
            !_strings.TryGetId(shardKey, out var shardKeyId) ||
            !TryGetBucketSet(typeof(T), out var bucketSet))
            return null;

        foreach (var shard in bucketSet.GetModuleView(moduleId).Shards)
        {
            if (shard.Key.ShardKeyId == shardKeyId)
                return ResolveMeta(shard.Key.SourceLocationId);
        }

        return null;
    }

    public Meta GetSourceLocation(int id) => ResolveMeta(id);

    private struct ShardMatch
    {
        public Bucket Bucket;
        public BucketView View;
        public Meta Meta;
        public int Index;
        public int End;
        public long Timestamp; // timestamp of the record at Index

        public void Advance()
        {
            Index++;
            if (Index < End)
                Timestamp = View.GetRecord(Index).Timestamp;
        }
    }

    /// <summary>
    /// Where a query puts its matches. A struct implementation keeps the query generic over
    /// its destination — the GUI fills a caller-owned list, the journal folds entries in as
    /// they arrive — with no delegate call and no intermediate buffer.
    /// </summary>
    private interface IEntrySink<T> where T : struct, IEntry
    {
        void Add(in T entry);
    }

    private readonly struct ListSink<T>(List<T> destination) : IEntrySink<T> where T : struct, IEntry
    {
        public void Add(in T entry) => destination.Add(entry);
    }

    private int QueryCore<T, TSink>(
        TSink sink,
        long t0,
        long t1,
        int? moduleId,
        int? sourceLocationId,
        int? shardKeyId,
        int? maxCount,
        Func<Meta, bool>? metaFilter) where T : struct, IEntry where TSink : IEntrySink<T>
    {
        if (!TryGetBucketSet(typeof(T), out var bucketSet))
            return 0;

        if (maxCount <= 0)
            return 0;

        var candidates = moduleId.HasValue
            ? bucketSet.GetModuleView(moduleId.Value).Shards
            : bucketSet.Shards;

        if (candidates.Length == 0)
            return 0;

        var matches = ArrayPool<ShardMatch>.Shared.Rent(candidates.Length);
        var heap = ArrayPool<int>.Shared.Rent(candidates.Length);
        try
        {
            // Collect shards that overlap [t0, t1].
            var matchCount = 0;
            var totalRecords = 0L;
            foreach (var shard in candidates)
            {
                if (sourceLocationId.HasValue && shard.Key.SourceLocationId != sourceLocationId.Value)
                    continue;

                if (shardKeyId.HasValue && shard.Key.ShardKeyId != shardKeyId.Value)
                    continue;

                var meta = ResolveMeta(shard.Key.SourceLocationId);
                if (metaFilter is not null && !metaFilter(meta))
                    continue;

                var bucket = shard.Bucket;
                var view = bucket.View;
                var count = view.RecordCount;
                var lo = view.LowerBound(t0, count);
                var hi = view.UpperBound(t1, count);
                if (lo >= hi)
                    continue;

                matches[matchCount++] = new ShardMatch
                {
                    Bucket = bucket,
                    View = view,
                    Meta = meta,
                    Index = lo,
                    End = hi,
                    Timestamp = view.GetRecord(lo).Timestamp,
                };
                totalRecords += hi - lo;
            }

            if (matchCount == 0)
                return 0;

            // When the window holds more records than requested, keep one per time bucket.
            var bucketSizeNs = 0L;
            if (maxCount.HasValue && totalRecords > maxCount.Value)
                bucketSizeNs = System.Math.Max(1, (t1 - t0) / maxCount.Value);

            var added = 0;
            var lastBucket = long.MinValue;

            if (matchCount == 1)
            {
                ref var match = ref matches[0];
                for (; match.Index < match.End; match.Index++)
                {
                    var record = match.View.GetRecord(match.Index);
                    if (bucketSizeNs > 0)
                    {
                        var bucket = record.Timestamp / bucketSizeNs;
                        if (bucket == lastBucket) continue;
                        lastBucket = bucket;
                    }

                    if (TryDeserialize(record, match.View, match.Bucket, match.Meta, out T entry))
                    {
                        sink.Add(entry);
                        added++;
                    }
                }

                return added;
            }

            // k-way merge by timestamp over a binary min-heap of match indices.
            var merge = new MergeHeap(matches, heap, matchCount);
            while (merge.TryPop(out var matchIndex))
            {
                ref var match = ref matches[matchIndex];
                var record = match.View.GetRecord(match.Index);
                match.Advance();
                if (match.Index < match.End)
                    merge.Push(matchIndex);

                if (bucketSizeNs > 0)
                {
                    var bucket = record.Timestamp / bucketSizeNs;
                    if (bucket == lastBucket) continue;
                    lastBucket = bucket;
                }

                if (TryDeserialize(record, match.View, match.Bucket, match.Meta, out T entry))
                {
                    sink.Add(entry);
                    added++;
                }
            }

            return added;
        }
        finally
        {
            ArrayPool<ShardMatch>.Shared.Return(matches, clearArray: true);
            ArrayPool<int>.Shared.Return(heap);
        }
    }

    /// <summary>Min-heap of indices into a ShardMatch array, keyed by each match's current timestamp.</summary>
    private ref struct MergeHeap
    {
        private readonly ShardMatch[] _matches;
        private readonly int[] _heap;
        private int _count;

        public MergeHeap(ShardMatch[] matches, int[] heap, int matchCount)
        {
            _matches = matches;
            _heap = heap;
            _count = 0;
            for (var i = 0; i < matchCount; i++)
                Push(i);
        }

        private long KeyAt(int heapIndex) => _matches[_heap[heapIndex]].Timestamp;

        public void Push(int matchIndex)
        {
            var i = _count++;
            _heap[i] = matchIndex;
            while (i > 0)
            {
                var parent = (i - 1) >> 1;
                if (KeyAt(parent) <= KeyAt(i)) break;
                (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
                i = parent;
            }
        }

        public bool TryPop(out int matchIndex)
        {
            if (_count == 0)
            {
                matchIndex = -1;
                return false;
            }

            matchIndex = _heap[0];
            _count--;
            if (_count == 0)
                return true;

            _heap[0] = _heap[_count];
            var i = 0;
            while (true)
            {
                var left = 2 * i + 1;
                if (left >= _count) break;
                var right = left + 1;
                var smallest = right < _count && KeyAt(right) < KeyAt(left) ? right : left;
                if (KeyAt(i) <= KeyAt(smallest)) break;
                (_heap[smallest], _heap[i]) = (_heap[i], _heap[smallest]);
                i = smallest;
            }

            return true;
        }
    }

    private static bool TryDeserialize<T>(in InternalRecord record, BucketView view, Bucket bucket, Meta meta, out T entry) where T : struct, IEntry
    {
        if (!view.TryGetBlob(record, out var blob))
        {
            // The records and blobs files grow independently, so a view taken before a
            // blobs-only growth can address a record whose blob lies past its own blob
            // mapping. Record indices are stable across views; retry on the current one.
            var current = bucket.View;
            if (ReferenceEquals(current, view) || !current.TryGetBlob(record, out blob))
            {
                Log.ZLogWarning(
                    $"DebugDb: {typeof(T).FullName} record at timestamp {record.Timestamp} points outside its blob file. Skipping.");
                entry = default;
                return false;
            }
        }

        try
        {
            entry = MemoryPackSerializer.Deserialize<T>(blob);
        }
        catch (Exception ex)
        {
            Log.ZLogWarning(ex,
                $"DebugDb: failed to deserialize {typeof(T).FullName} at timestamp {record.Timestamp}. " +
                $"Skipping corrupt or incompatible row.");
            entry = default;
            return false;
        }

        entry.Timestamp = Timestamp.FromNanoseconds(record.Timestamp);
        entry.Meta = meta;
        return true;
    }

    // ─── Bucket sets ────────────────────────────────────────────────────────

    private BucketSet GetOrCreateBucketSet(Type type)
    {
        return _buckets.GetOrAdd(
            type,
            static (bucketType, db) => new Lazy<BucketSet>(
                () => db.LoadBucketsForType(bucketType),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this).Value;
    }

    private bool TryGetBucketSet(Type type, out BucketSet bucketSet)
    {
        if (_buckets.TryGetValue(type, out var lazy))
        {
            bucketSet = lazy.Value;
            return true;
        }

        bucketSet = null!;
        return false;
    }

    private BucketSet LoadBucketsForType(Type type)
    {
        var typeDirectory = GetTypeDirectory(type);
        Directory.CreateDirectory(typeDirectory);

        var bucketSet = new BucketSet(this, typeDirectory);
        foreach (var recordsPath in Directory.GetFiles(typeDirectory, "*.records"))
        {
            if (TryParseShardName(type, recordsPath, out var shard))
                bucketSet.GetOrAdd(shard);
        }

        return bucketSet;
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
                value = 0;
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

    /// <summary>One shard: a key and its lazily opened bucket.</summary>
    private sealed class Shard(EntryShardKey key, string directory)
    {
        private Bucket? _bucket;
        private readonly Lock _openLock = new();

        public EntryShardKey Key { get; } = key;

        public Bucket Bucket
        {
            get
            {
                var bucket = Volatile.Read(ref _bucket);
                if (bucket is not null)
                    return bucket;

                lock (_openLock)
                {
                    bucket = _bucket;
                    if (bucket is null)
                    {
                        bucket = new Bucket(directory, GetShardName(Key));
                        Volatile.Write(ref _bucket, bucket);
                    }

                    return bucket;
                }
            }
        }

        public void Dispose() => Volatile.Read(ref _bucket)?.Dispose();
    }

    /// <summary>Shards of one module, plus the distinct source locations and shard keys they cover.</summary>
    private sealed class ModuleView(Shard[] source, Shard[] shards, int[] sourceLocationIds, string[] shardKeys)
    {
        /// <summary>The full shard array this view was built from; a different array means the view is stale.</summary>
        public Shard[] Source { get; } = source;
        public Shard[] Shards { get; } = shards;
        public int[] SourceLocationIds { get; } = sourceLocationIds;
        public string[] ShardKeys { get; } = shardKeys;
    }

    /// <summary>
    /// All shards of one entry type. Shards are added rarely (first time a new
    /// (module, source, key) tuple appears), so the shard list is a copy-on-write
    /// array and per-module views are rebuilt lazily after each addition.
    /// </summary>
    private sealed class BucketSet(DebugDb db, string directory)
    {
        private readonly ConcurrentDictionary<EntryShardKey, Shard> _byKey = new();
        private readonly ConcurrentDictionary<int, ModuleView> _moduleViews = new();
        private readonly Lock _addLock = new();
        private Shard[] _shards = [];

        public Shard[] Shards => Volatile.Read(ref _shards);

        public Shard GetOrAdd(EntryShardKey key)
        {
            if (_byKey.TryGetValue(key, out var existing))
                return existing;

            lock (_addLock)
            {
                if (_byKey.TryGetValue(key, out existing))
                    return existing;

                var shard = new Shard(key, directory);
                var shards = _shards;
                var grown = new Shard[shards.Length + 1];
                Array.Copy(shards, grown, shards.Length);
                grown[^1] = shard;

                _byKey[key] = shard;
                Volatile.Write(ref _shards, grown);
                return shard;
            }
        }

        /// <summary>
        /// Per-module view, rebuilt whenever the shard array it was built from has been
        /// replaced. Checking the source array (rather than clearing a cache on add) means a
        /// view built from a stale array during an add can never be cached past the add.
        /// </summary>
        public ModuleView GetModuleView(int moduleId)
        {
            var source = Shards;
            if (_moduleViews.TryGetValue(moduleId, out var view) && ReferenceEquals(view.Source, source))
                return view;

            view = BuildModuleView(moduleId, source);
            _moduleViews[moduleId] = view;
            return view;
        }

        private ModuleView BuildModuleView(int moduleId, Shard[] source)
        {
            var shards = new List<Shard>();
            var sourceLocationIds = new SortedSet<int>();
            var shardKeys = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var shard in source)
            {
                if (shard.Key.ModuleId != moduleId)
                    continue;

                shards.Add(shard);
                sourceLocationIds.Add(shard.Key.SourceLocationId);

                if (shard.Key.ShardKeyId >= 0 && db._strings.Get(shard.Key.ShardKeyId) is { } shardKey)
                    shardKeys.Add(shardKey);
            }

            return new ModuleView(source, [.. shards], [.. sourceLocationIds], [.. shardKeys]);
        }

        public void Dispose()
        {
            foreach (var shard in Shards)
                shard.Dispose();
        }
    }

    // ─── Journal ────────────────────────────────────────────────────────────

    /// <summary>Folds log entries straight into the journal as the query produces them.</summary>
    private readonly struct JournalSink(DebugDb db) : IEntrySink<Entry>
    {
        public void Add(in Entry entry)
        {
            if (entry.Level >= JournalMinLevel)
                db.AddToJournal(entry);
        }
    }

    public void BuildJournal(bool group = true)
    {
        var range = GetFrameRange();
        if (range is null) return;

        lock (_journalLock)
        {
            _groupJournalEntries = group;
            _journal.Clear();
            _journalIndex.Clear();

            // Streamed, not materialized: the k-way merge already yields entries oldest first,
            // so the journal can be folded as they arrive instead of buffering every log entry
            // of the session (and then sorting an already-sorted list) first.
            QueryCore<Entry, JournalSink>(
                new JournalSink(this),
                range.Value.Start.Nanoseconds, range.Value.End.Nanoseconds,
                moduleId: null, sourceLocationId: null, shardKeyId: null, maxCount: null, metaFilter: null);
        }
    }

    public void RebuildJournal(bool group) => BuildJournal(group);

    public void FillJournal(List<JournalGroup> destination)
    {
        lock (_journalLock)
        {
            destination.Clear();
            destination.AddRange(_journal);
        }
    }

    // ─── Frames ─────────────────────────────────────────────────────────────

    public void AppendFrame(Frame frame)
    {
        int moduleId;
        if (!_moduleIds.TryGetValue(frame.ModuleName, out moduleId))
        {
            lock (_internLock)
                moduleId = _strings.Intern(frame.ModuleName);

            _moduleIds.TryAdd(frame.ModuleName, moduleId);
        }

        _frames.Append(new InternalFrame
        {
            Timestamp = frame.StartTimestamp.Nanoseconds,
            ModuleId = moduleId,
        });

        GetOrCreateFrameIndex(moduleId).Add(frame.StartTimestamp.Nanoseconds);
    }

    public (Timestamp Start, Timestamp End)? GetFrameRange()
    {
        var frames = _frames.View;
        var count = frames.RecordCount;
        if (count == 0)
            return null;

        var start = Timestamp.FromNanoseconds(frames.GetRecord(0).Timestamp);
        var end = Timestamp.FromNanoseconds(frames.GetRecord(count - 1).Timestamp);
        return (start, end);
    }

    public IEnumerable<Frame> QueryFrames(string module, Timestamp t0, Timestamp t1)
    {
        if (!_moduleIds.TryGetValue(module, out var moduleId) || !_frameIndices.TryGetValue(moduleId, out var frameIndex))
            return [];

        var (timestamps, count) = frameIndex.Snapshot();
        var lo = ModuleFrameIndex.LowerBound(timestamps, count, t0.Nanoseconds);
        var hi = ModuleFrameIndex.UpperBound(timestamps, count, t1.Nanoseconds);
        if (lo >= hi)
            return [];

        var frames = new Frame[hi - lo];
        for (var i = lo; i < hi; i++)
        {
            frames[i - lo] = new Frame
            {
                ModuleName = module,
                StartTimestamp = Timestamp.FromNanoseconds(timestamps[i]),
            };
        }

        return frames;
    }

    public (Timestamp Start, Timestamp End)? GetFrameAt(string module, Timestamp t)
    {
        if (!_moduleIds.TryGetValue(module, out var moduleId) || !_frameIndices.TryGetValue(moduleId, out var frameIndex))
            return null;

        var (timestamps, count) = frameIndex.Snapshot();
        var pos = ModuleFrameIndex.UpperBound(timestamps, count, t.Nanoseconds);
        if (pos == 0)
            return null;

        var start = Timestamp.FromNanoseconds(timestamps[pos - 1]);
        var end = pos < count ? Timestamp.FromNanoseconds(timestamps[pos]) : Timestamp.MaxValue;
        return (start, end);
    }

    private ModuleFrameIndex GetOrCreateFrameIndex(int moduleId)
    {
        return _frameIndices.GetOrAdd(moduleId, static _ => new ModuleFrameIndex());
    }

    /// <summary>
    /// Sorted frame start timestamps of one module. Single writer; readers take a
    /// (array, count) snapshot and binary search it without locking.
    /// </summary>
    private sealed class ModuleFrameIndex
    {
        private readonly Lock _lock = new();
        private long[] _timestamps = new long[256];
        private int _count;

        public void Add(long timestamp)
        {
            lock (_lock)
            {
                var timestamps = _timestamps;
                if (_count == timestamps.Length)
                {
                    var grown = new long[timestamps.Length * 2];
                    Array.Copy(timestamps, grown, timestamps.Length);
                    Volatile.Write(ref _timestamps, grown);
                    timestamps = grown;
                }

                timestamps[_count] = timestamp;
                Volatile.Write(ref _count, _count + 1);
            }
        }

        public (long[] Timestamps, int Count) Snapshot()
        {
            // Count first: the array read afterwards is at least as new as the count.
            var count = Volatile.Read(ref _count);
            var timestamps = Volatile.Read(ref _timestamps);
            return (timestamps, count);
        }

        public static int LowerBound(long[] timestamps, int count, long value)
        {
            var lo = 0;
            var hi = count;
            while (lo < hi)
            {
                var mid = lo + (hi - lo) / 2;
                if (timestamps[mid] < value) lo = mid + 1;
                else hi = mid;
            }

            return lo;
        }

        public static int UpperBound(long[] timestamps, int count, long value)
        {
            var lo = 0;
            var hi = count;
            while (lo < hi)
            {
                var mid = lo + (hi - lo) / 2;
                if (timestamps[mid] <= value) lo = mid + 1;
                else hi = mid;
            }

            return lo;
        }
    }

    // ─── Lifetime ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        foreach (var lazy in _buckets.Values)
        {
            if (lazy.IsValueCreated)
                lazy.Value.Dispose();
        }

        _frames.Dispose();
        _sources.Dispose();
        _strings.Dispose();
    }
}

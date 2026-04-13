using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LightningDB;
using MemoryPack;
using Tyr.Common.Time;

namespace Tyr.Common.Debug.Db;

[MemoryPackable]
internal partial record struct InternalMeta(
    int ModuleId,
    int LayerId,
    int FileId,
    int MemberId,
    int Line,
    int ExpressionId
);

[MemoryPackable]
internal partial record struct BucketDef(string TypeName, int ModuleId, int SourceId, int ShardKeyId);

public sealed class DebugDb : IDebugDb
{
    private readonly string _directory;
    private readonly LightningEnvironment _env;
    private readonly ReaderWriterLockSlim _txLock = new();

    private readonly ConcurrentDictionary<string, int> _stringIds = new();
    private readonly ConcurrentDictionary<int, string> _strings = new();

    private readonly ConcurrentDictionary<Meta, int> _sourceIds = new();
    private readonly ConcurrentDictionary<int, Meta> _sources = new();

    private readonly ConcurrentDictionary<BucketDef, int> _bucketIds = new();
    private readonly ConcurrentDictionary<int, BucketDef> _buckets = new();

    private readonly ConcurrentDictionary<int, ModuleFrameIndex> _frameIndices = new();

    private LightningDatabase _metaDb = null!;
    private readonly ConcurrentDictionary<string, LightningDatabase> _typeDbs = new();

    private int _nextStringId;
    private int _nextSourceId;
    private int _nextBucketId;
    private long _currentMapSize;

    private readonly Lock _internLock = new();
    private readonly Lock _writeLock = new();
    private LightningTransaction? _currentWriteTx;

    [ThreadStatic] private static System.Buffers.ArrayBufferWriter<byte>? _serializeBuffer;

    private const byte MetaPrefixString = 3;
    private const byte MetaPrefixSource = 5;
    private const byte MetaPrefixBucket = 7;
    private const byte MetaPrefixFrame = 8;

    public DebugDb(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);

        _env = new LightningEnvironment(directory)
        {
            MaxDatabases = 256,
            MapSize = 1 * 1024 * 1024, // 16 MB initial
        };
        _env.Open(EnvironmentOpenFlags.NoSync | EnvironmentOpenFlags.NoMetaSync | EnvironmentOpenFlags.WriteMap | EnvironmentOpenFlags.MapAsync);
        _currentMapSize = _env.Info.MapSize;

        RunWrite(tx =>
        {
            _metaDb = tx.OpenDatabase("__meta", new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create });
            return 0;
        });

        LoadMetadata();
    }

    private void LoadMetadata()
    {
        RunRead(tx =>
        {
            using var cursor = tx.CreateCursor(_metaDb);
            var (result, key, value) = cursor.First();
            while (result == MDBResultCode.Success)
            {
                var keySpan = key.AsSpan();
                var valueSpan = value.AsSpan();
                var prefix = keySpan[0];

                if (prefix == MetaPrefixString)
                {
                    var id = BinaryPrimitives.ReadInt32BigEndian(keySpan[1..]);
                    var str = Encoding.UTF8.GetString(valueSpan);
                    _stringIds[str] = id;
                    _strings[id] = str;
                    if (id >= _nextStringId) _nextStringId = id + 1;
                }
                else if (prefix == MetaPrefixSource)
                {
                    var id = BinaryPrimitives.ReadInt32BigEndian(keySpan[1..]);
                    var internalMeta = MemoryPackSerializer.Deserialize<InternalMeta>(valueSpan);
                    var meta = new Meta
                    {
                        Module = _strings[internalMeta.ModuleId],
                        Layer = _strings[internalMeta.LayerId],
                        File = internalMeta.FileId >= 0 ? _strings[internalMeta.FileId] : null,
                        Member = internalMeta.MemberId >= 0 ? _strings[internalMeta.MemberId] : null,
                        Line = internalMeta.Line,
                        Expression = internalMeta.ExpressionId >= 0 ? _strings[internalMeta.ExpressionId] : null
                    };
                    _sourceIds[meta] = id;
                    _sources[id] = meta;
                    if (id >= _nextSourceId) _nextSourceId = id + 1;
                }
                else if (prefix == MetaPrefixBucket)
                {
                    var id = BinaryPrimitives.ReadInt32BigEndian(keySpan[1..]);
                    var def = MemoryPackSerializer.Deserialize<BucketDef>(valueSpan);
                    _bucketIds[def] = id;
                    _buckets[id] = def;
                    if (id >= _nextBucketId) _nextBucketId = id + 1;
                }
                else if (prefix == MetaPrefixFrame)
                {
                    var moduleId = BinaryPrimitives.ReadInt32BigEndian(keySpan[1..5]);
                    var timestamp = BinaryPrimitives.ReadInt64BigEndian(keySpan[5..13]);
                    GetOrCreateFrameIndex(moduleId).Add(timestamp);
                }

                (result, key, value) = cursor.Next();
            }
            return 0;
        });
    }

    private TResult RunWrite<TResult>(Func<LightningTransaction, TResult> action)
    {
        if (_currentWriteTx != null)
            return action(_currentWriteTx);

        lock (_writeLock)
        {
            while (true)
            {
                _txLock.EnterReadLock();
                try
                {
                    using var tx = _env.BeginTransaction();
                    try
                    {
                        var result = action(tx);
                        CheckResult(tx.Commit());
                        return result;
                    }
                    catch (MapFullException)
                    {
                    }
                    catch (LightningException ex) when (ex.StatusCode == (int)MDBResultCode.MapFull)
                    {
                    }
                }
                finally
                {
                    _txLock.ExitReadLock();
                }

                GrowMapSize();
            }
        }
    }

    public void RunBatch(Action action)
    {
        lock (_writeLock)
        {
            while (true)
            {
                _txLock.EnterReadLock();
                try
                {
                    using var tx = _env.BeginTransaction();
                    _currentWriteTx = tx;
                    try
                    {
                        action();
                        CheckResult(tx.Commit());
                        return;
                    }
                    catch (MapFullException)
                    {
                    }
                    catch (LightningException ex) when (ex.StatusCode == (int)MDBResultCode.MapFull)
                    {
                    }
                    finally
                    {
                        _currentWriteTx = null;
                    }
                }
                finally
                {
                    _txLock.ExitReadLock();
                }

                GrowMapSize();
            }
        }
    }

    private static void CheckResult(MDBResultCode result)
    {
        if (result == MDBResultCode.MapFull)
            throw new MapFullException();
        if (result != MDBResultCode.Success)
            throw new InvalidOperationException($"LightningDB operation failed: {result}");
    }

    private sealed class MapFullException : Exception { }

    private void GrowMapSize()
    {
        _txLock.EnterWriteLock();
        try
        {
            var info = _env.Info;
            if (info.MapSize == _currentMapSize)
            {
                _currentMapSize = info.MapSize * 2;
                _env.MapSize = _currentMapSize;
            }
        }
        finally
        {
            _txLock.ExitWriteLock();
        }
    }

    private TResult RunRead<TResult>(Func<LightningTransaction, TResult> action)
    {
        _txLock.EnterReadLock();
        try
        {
            using var tx = _env.BeginTransaction(TransactionBeginFlags.ReadOnly);
            return action(tx);
        }
        finally
        {
            _txLock.ExitReadLock();
        }
    }

    public DebugDb RegisterType<T>() where T : struct, IEntry
    {
        return RegisterType(typeof(T));
    }

    public DebugDb RegisterType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!type.IsValueType || !typeof(IEntry).IsAssignableFrom(type))
            throw new ArgumentException($"Debug entry type {type.FullName} must be a value type implementing {nameof(IEntry)}.", nameof(type));

        GetOrCreateTypeDb(GetTypeName(type));
        return this;
    }

    private string GetTypeName(Type type)
    {
        var typeName = type.FullName ?? type.Name;
        return typeName.Replace('+', '_');
    }

    private LightningDatabase GetOrCreateTypeDb(string typeName)
    {
        return _typeDbs.GetOrAdd(typeName, name =>
        {
            return RunWrite(tx =>
            {
                return tx.OpenDatabase(name, new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create });
            });
        });
    }

    public void Append<T>(T entry) where T : struct, IEntry
    {
        var meta = entry.Meta;
        var module = meta.Module ?? "";
        var shardKey = entry.ShardKey;
        var typeName = GetTypeName(typeof(T));

        int moduleId = 0, sourceId = 0, shardKeyId = 0, bucketId = 0;

        if (!_stringIds.TryGetValue(module, out moduleId) ||
            !_sourceIds.TryGetValue(meta, out sourceId) ||
            !TryGetShardKeyId(shardKey, out shardKeyId) ||
            !_bucketIds.TryGetValue(new BucketDef(typeName, moduleId, sourceId, shardKeyId), out bucketId))
        {
            lock (_internLock)
            {
                if (!_stringIds.TryGetValue(module, out moduleId) ||
                    !_sourceIds.TryGetValue(meta, out sourceId) ||
                    !TryGetShardKeyId(shardKey, out shardKeyId) ||
                    !_bucketIds.TryGetValue(new BucketDef(typeName, moduleId, sourceId, shardKeyId), out bucketId))
                {
                    RunWrite(tx =>
                    {
                        var addedStrings = new List<string>();
                        var addedSources = new List<Meta>();
                        var addedBuckets = new List<BucketDef>();
                        int origStringId = _nextStringId;
                        int origSourceId = _nextSourceId;
                        int origBucketId = _nextBucketId;

                        try
                        {
                            if (!_stringIds.TryGetValue(module, out moduleId))
                                moduleId = InternString(tx, module, addedStrings);

                            if (!_sourceIds.TryGetValue(meta, out sourceId))
                                sourceId = InternSource(tx, meta, addedStrings, addedSources);

                            if (shardKey != null && !_stringIds.TryGetValue(shardKey, out shardKeyId))
                                shardKeyId = InternString(tx, shardKey, addedStrings);
                            else if (shardKey == null)
                                shardKeyId = -1;

                            var def = new BucketDef(typeName, moduleId, sourceId, shardKeyId);
                            if (!_bucketIds.TryGetValue(def, out bucketId))
                                bucketId = InternBucket(tx, def, addedBuckets);

                            return 0;
                        }
                        catch
                        {
                            foreach (var s in addedStrings) { _stringIds.TryRemove(s, out var id); _strings.TryRemove(id, out _); }
                            foreach (var s in addedSources) { _sourceIds.TryRemove(s, out var id); _sources.TryRemove(id, out _); }
                            foreach (var b in addedBuckets) { _bucketIds.TryRemove(b, out var id); _buckets.TryRemove(id, out _); }
                            _nextStringId = origStringId;
                            _nextSourceId = origSourceId;
                            _nextBucketId = origBucketId;
                            throw;
                        }
                    });
                }
            }
        }

        var buffer = _serializeBuffer ??= new System.Buffers.ArrayBufferWriter<byte>();
        buffer.ResetWrittenCount();
        MemoryPackSerializer.Serialize(buffer, entry);
        var valueSpan = buffer.WrittenSpan;

        var typeDb = GetOrCreateTypeDb(typeName);

        Span<byte> keySpan = stackalloc byte[12];
        BinaryPrimitives.WriteInt32BigEndian(keySpan, bucketId);
        BinaryPrimitives.WriteInt64BigEndian(keySpan[4..], entry.Timestamp.Nanoseconds);

        if (_currentWriteTx != null)
        {
            CheckResult(_currentWriteTx.Put(typeDb, keySpan, valueSpan));
        }
        else
        {
            byte[] keyArr = keySpan.ToArray();
            byte[] valArr = valueSpan.ToArray();
            RunWrite(tx =>
            {
                CheckResult(tx.Put(typeDb, keyArr, valArr));
                return 0;
            });
        }
    }

    private int InternString(LightningTransaction tx, string str, List<string>? addedStrings = null)
    {
        var id = _nextStringId++;
        Span<byte> key = stackalloc byte[5];
        key[0] = MetaPrefixString;
        BinaryPrimitives.WriteInt32BigEndian(key[1..], id);
        
        var bytes = Encoding.UTF8.GetBytes(str);
        CheckResult(tx.Put(_metaDb, key, bytes));
        
        _stringIds[str] = id;
        _strings[id] = str;
        addedStrings?.Add(str);
        return id;
    }

    private int InternSource(LightningTransaction tx, Meta meta, List<string>? addedStrings = null, List<Meta>? addedSources = null)
    {
        var moduleId = _stringIds.TryGetValue(meta.Module, out var m) ? m : InternString(tx, meta.Module, addedStrings);
        var layerId = _stringIds.TryGetValue(meta.Layer, out var l) ? l : InternString(tx, meta.Layer, addedStrings);
        var fileId = meta.File != null ? (_stringIds.TryGetValue(meta.File, out var f) ? f : InternString(tx, meta.File, addedStrings)) : -1;
        var memberId = meta.Member != null ? (_stringIds.TryGetValue(meta.Member, out var mem) ? mem : InternString(tx, meta.Member, addedStrings)) : -1;
        var exprId = meta.Expression != null ? (_stringIds.TryGetValue(meta.Expression, out var e) ? e : InternString(tx, meta.Expression, addedStrings)) : -1;

        var internalMeta = new InternalMeta(moduleId, layerId, fileId, memberId, meta.Line, exprId);

        var id = _nextSourceId++;
        Span<byte> key = stackalloc byte[5];
        key[0] = MetaPrefixSource;
        BinaryPrimitives.WriteInt32BigEndian(key[1..], id);
        
        var buffer = _serializeBuffer ??= new System.Buffers.ArrayBufferWriter<byte>();
        buffer.ResetWrittenCount();
        MemoryPackSerializer.Serialize(buffer, internalMeta);
        CheckResult(tx.Put(_metaDb, key, buffer.WrittenSpan));
        
        _sourceIds[meta] = id;
        _sources[id] = meta;
        addedSources?.Add(meta);
        return id;
    }

    private int InternBucket(LightningTransaction tx, BucketDef def, List<BucketDef>? addedBuckets = null)
    {
        var id = _nextBucketId++;
        Span<byte> key = stackalloc byte[5];
        key[0] = MetaPrefixBucket;
        BinaryPrimitives.WriteInt32BigEndian(key[1..], id);
        
        var buffer = _serializeBuffer ??= new System.Buffers.ArrayBufferWriter<byte>();
        buffer.ResetWrittenCount();
        MemoryPackSerializer.Serialize(buffer, def);
        CheckResult(tx.Put(_metaDb, key, buffer.WrittenSpan));
        
        _bucketIds[def] = id;
        _buckets[id] = def;
        addedBuckets?.Add(def);
        return id;
    }

    private bool TryGetShardKeyId(string? shardKey, out int shardKeyId)
    {
        if (shardKey is null)
        {
            shardKeyId = -1;
            return true;
        }
        return _stringIds.TryGetValue(shardKey, out shardKeyId);
    }

    public IEnumerable<T> Query<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
    {
        if (!_stringIds.TryGetValue(module, out var moduleId))
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

    public IEnumerable<T> QueryWithoutMeta<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
    {
        if (!_stringIds.TryGetValue(module, out var moduleId))
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
        int? maxCount = null) where T : struct, IEntry
    {
        int? moduleId = null;
        if (module is not null)
        {
            if (!_stringIds.TryGetValue(module, out var resolvedModuleId))
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

    public IEnumerable<T> QueryAll<T>(Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
    {
        foreach (var entry in Query<T>(t0, t1, null, null, shardKey, maxCount))
            yield return entry;
    }

    public IEnumerable<string> QueryModules()
    {
        foreach (var module in _stringIds.Keys.Order())
            yield return module;
    }

    public IEnumerable<string> QueryShardKeys<T>(string module) where T : struct, IEntry
    {
        if (!_stringIds.TryGetValue(module, out var moduleId))
            yield break;

        var typeName = GetTypeName(typeof(T));
        var shardKeys = _bucketIds.Keys
            .Where(b => b.TypeName == typeName && b.ModuleId == moduleId && b.ShardKeyId >= 0)
            .Select(b => b.ShardKeyId)
            .Distinct()
            .Select(id => _strings.GetValueOrDefault(id))
            .OfType<string>()
            .Order();

        foreach (var shardKey in shardKeys)
            yield return shardKey;
    }

    public IEnumerable<Meta> QuerySourceLocations<T>(string module) where T : struct, IEntry
    {
        if (!_stringIds.TryGetValue(module, out var moduleId))
            yield break;

        var typeName = GetTypeName(typeof(T));
        var sourceIds = _bucketIds.Keys
            .Where(b => b.TypeName == typeName && b.ModuleId == moduleId)
            .Select(b => b.SourceId)
            .Distinct()
            .Order();

        foreach (var sourceId in sourceIds)
        {
            if (_sources.TryGetValue(sourceId, out var meta))
                yield return meta;
        }
    }

    public Meta? TryGetShardMeta<T>(string module, string shardKey) where T : struct, IEntry
    {
        if (!_stringIds.TryGetValue(module, out var moduleId))
            return null;

        if (!TryGetShardKeyId(shardKey, out var shardKeyId))
            return null;

        var typeName = GetTypeName(typeof(T));
        var bucket = _bucketIds.Keys.FirstOrDefault(b => b.TypeName == typeName && b.ModuleId == moduleId && b.ShardKeyId == shardKeyId);
        
        if (bucket.TypeName != null && _sources.TryGetValue(bucket.SourceId, out var meta))
            return meta;

        return null;
    }

    private IEnumerable<T> QueryCore<T>(
        Timestamp t0,
        Timestamp t1,
        int? moduleId,
        int? sourceLocationId,
        int? shardKeyId,
        int? maxCount,
        bool hydrateMeta) where T : struct, IEntry
    {
        var typeName = GetTypeName(typeof(T));
        var candidates = _bucketIds.Keys
            .Where(b => b.TypeName == typeName &&
                        (!moduleId.HasValue || b.ModuleId == moduleId.Value) &&
                        (!sourceLocationId.HasValue || b.SourceId == sourceLocationId.Value) &&
                        (!shardKeyId.HasValue || b.ShardKeyId == shardKeyId.Value))
            .Select(b => _bucketIds[b])
            .ToList();

        if (candidates.Count == 0)
            yield break;

        if (maxCount <= 0)
            yield break;

        var db = GetOrCreateTypeDb(typeName);

        _txLock.EnterReadLock();
        try
        {
            using var tx = _env.BeginTransaction(TransactionBeginFlags.ReadOnly);
            
            var cursors = new List<LightningCursor>();
            Span<byte> key = stackalloc byte[12];
            foreach (var bucketId in candidates)
            {
                var cursor = tx.CreateCursor(db);
                BinaryPrimitives.WriteInt32BigEndian(key, bucketId);
                BinaryPrimitives.WriteInt64BigEndian(key[4..], t0.Nanoseconds);
                
                var (result, currentKey, currentValue) = cursor.SetKey(key);
                if (result == MDBResultCode.NotFound)
                {
                    result = cursor.SetRange(key);
                    if (result == MDBResultCode.Success)
                    {
                        (result, currentKey, currentValue) = cursor.GetCurrent();
                    }
                }

                if (result == MDBResultCode.Success && currentKey.AsSpan().Length == 12)
                {
                    var curBucketId = BinaryPrimitives.ReadInt32BigEndian(currentKey.AsSpan());
                    if (curBucketId == bucketId)
                    {
                        var ts = BinaryPrimitives.ReadInt64BigEndian(currentKey.AsSpan()[4..]);
                        if (ts <= t1.Nanoseconds)
                        {
                            cursors.Add(cursor);
                        }
                        else
                        {
                            cursor.Dispose();
                        }
                    }
                    else
                    {
                        cursor.Dispose();
                    }
                }
                else
                {
                    cursor.Dispose();
                }
            }

            if (cursors.Count == 0)
                yield break;

            try
            {
                var queue = new PriorityQueue<CursorState, long>();
                foreach (var cursor in cursors)
                {
                    var (result, curKey, curValue) = cursor.GetCurrent();
                    if (result == MDBResultCode.Success)
                    {
                        var ts = BinaryPrimitives.ReadInt64BigEndian(curKey.AsSpan()[4..]);
                        queue.Enqueue(new CursorState(cursor, ts, curValue.AsSpan().ToArray(), curKey.AsSpan()[0..4].ToArray()), ts);
                    }
                }

                long rangeNs = t1.Nanoseconds - t0.Nanoseconds;
                long bucketSizeNs = maxCount.HasValue && maxCount.Value > 0 ? System.Math.Max(1, rangeNs / maxCount.Value) : 0;
                long lastBucket = long.MinValue;
                long totalYielded = 0;

                while (queue.TryDequeue(out var state, out var currentTs))
                {
                    bool shouldYield = true;
                    if (maxCount.HasValue && maxCount.Value > 0)
                    {
                        long bucket = currentTs / bucketSizeNs;
                        if (bucket == lastBucket)
                        {
                            shouldYield = false;
                        }
                        else
                        {
                            lastBucket = bucket;
                        }
                    }

                    if (shouldYield)
                    {
                        var entry = DeserializeEntry<T>(state.CurrentValue, state.CurrentBucketId, currentTs, hydrateMeta);
                        if (entry.HasValue)
                        {
                            yield return entry.Value;
                            totalYielded++;
                            if (maxCount.HasValue && totalYielded >= maxCount.Value)
                                break;
                        }
                    }

                    var (res, nextKey, nextValue) = state.Cursor.Next();
                    if (res == MDBResultCode.Success && nextKey.AsSpan().Length == 12)
                    {
                        var curBucketId = BinaryPrimitives.ReadInt32BigEndian(nextKey.AsSpan());
                        var bucketId = BinaryPrimitives.ReadInt32BigEndian(state.CurrentBucketId);
                        if (curBucketId == bucketId)
                        {
                            var ts = BinaryPrimitives.ReadInt64BigEndian(nextKey.AsSpan()[4..]);
                            if (ts <= t1.Nanoseconds)
                            {
                                state.CurrentValue = nextValue.AsSpan().ToArray();
                                state.Timestamp = ts;
                                queue.Enqueue(state, ts);
                            }
                        }
                    }
                }
            }
            finally
            {
                foreach (var c in cursors) c.Dispose();
            }
        }
        finally
        {
            _txLock.ExitReadLock();
        }
    }

    private sealed class CursorState
    {
        public LightningCursor Cursor { get; }
        public long Timestamp { get; set; }
        public byte[] CurrentValue { get; set; }
        public byte[] CurrentBucketId { get; }

        public CursorState(LightningCursor cursor, long timestamp, byte[] currentValue, byte[] currentBucketId)
        {
            Cursor = cursor;
            Timestamp = timestamp;
            CurrentValue = currentValue;
            CurrentBucketId = currentBucketId;
        }
    }

    private T? DeserializeEntry<T>(byte[] blob, byte[] bucketIdBytes, long timestampNs, bool hydrateMeta) where T : struct, IEntry
    {
        try
        {
            var bucketId = BinaryPrimitives.ReadInt32BigEndian(bucketIdBytes);
            var entry = MemoryPackSerializer.Deserialize<T>(blob);
            var value = entry;

            value.Timestamp = Timestamp.FromNanoseconds(timestampNs);

            if (hydrateMeta && _buckets.TryGetValue(bucketId, out var def) && _sources.TryGetValue(def.SourceId, out var meta))
            {
                value.Meta = meta;
            }
            else
            {
                value.Meta = Meta.Empty;
            }
            return value;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DebugDb warning: failed to deserialize {typeof(T).FullName}. Skipping corrupt or incompatible row. {ex}");
            return null;
        }
    }

    public Meta GetSourceLocation(int id)
    {
        return _sources.GetValueOrDefault(id, Meta.Empty);
    }

    public void AppendFrame(Frame frame)
    {
        int moduleId;
        lock (_internLock)
        {
            if (!_stringIds.TryGetValue(frame.ModuleName, out moduleId))
            {
                RunWrite(tx =>
                {
                    var addedStrings = new List<string>();
                    int origStringId = _nextStringId;

                    try
                    {
                        moduleId = InternString(tx, frame.ModuleName, addedStrings);
                        return 0;
                    }
                    catch
                    {
                        foreach (var s in addedStrings) { _stringIds.TryRemove(s, out var id); _strings.TryRemove(id, out _); }
                        _nextStringId = origStringId;
                        throw;
                    }
                });
            }
        }

        Span<byte> key = stackalloc byte[13];
        key[0] = MetaPrefixFrame;
        BinaryPrimitives.WriteInt32BigEndian(key[1..5], moduleId);
        BinaryPrimitives.WriteInt64BigEndian(key[5..13], frame.StartTimestamp.Nanoseconds);

        if (_currentWriteTx != null)
        {
            CheckResult(_currentWriteTx.Put(_metaDb, key, ReadOnlySpan<byte>.Empty));
        }
        else
        {
            byte[] keyArr = key.ToArray();
            RunWrite(tx =>
            {
                CheckResult(tx.Put(_metaDb, keyArr, ReadOnlySpan<byte>.Empty));
                return 0;
            });
        }

        GetOrCreateFrameIndex(moduleId).Add(frame.StartTimestamp.Nanoseconds);
    }

    public (Timestamp Start, Timestamp End)? GetFrameRange()
    {
        Timestamp? minStart = null;
        Timestamp? maxEnd = null;

        foreach (var frameIndex in _frameIndices.Values)
        {
            var (start, end) = frameIndex.GetGlobalRange();
            if (start.HasValue)
            {
                if (!minStart.HasValue || start.Value < minStart.Value)
                    minStart = start.Value;
                
                if (end.HasValue && (!maxEnd.HasValue || end.Value > maxEnd.Value))
                    maxEnd = end.Value;
            }
        }

        if (minStart.HasValue && maxEnd.HasValue)
            return (minStart.Value, maxEnd.Value);

        return null;
    }

    public IEnumerable<Frame> QueryFrames(string module, Timestamp t0, Timestamp t1)
    {
        if (!_stringIds.TryGetValue(module, out var moduleId))
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
        if (!_stringIds.TryGetValue(module, out var moduleId))
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
        foreach (var db in _typeDbs.Values)
        {
            db.Dispose();
        }
        _metaDb?.Dispose();
        _env.Dispose();
        _txLock.Dispose();
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

        public (Timestamp? Start, Timestamp? End) GetGlobalRange()
        {
            lock (_lock)
            {
                if (_timestamps.Count == 0) return (null, null);
                return (Timestamp.FromNanoseconds(_timestamps[0]), Timestamp.FromNanoseconds(_timestamps[^1]));
            }
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
                    start = 0;
                    end = null;
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
}

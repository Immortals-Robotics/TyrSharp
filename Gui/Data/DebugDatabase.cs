using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MemoryPack;
using Tyr.Common.Debug;

namespace Tyr.Gui.Data;

public interface IDebugDb : IDisposable
{
    void              Append<T>(T entry) where T : IEntry;
    IEnumerable<T>    Query<T>(string module, Timestamp t0, Timestamp t1) where T : IEntry;
    IEnumerable<T>    QueryAll<T>(Timestamp t0, Timestamp t1) where T : IEntry;
    Meta    GetMeta(int id);
}

// ─── Internal fixed-size record stored in the .records mmap ─────────────────

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct InternalRecord
{
    public long Timestamp;
    public int  ModuleId;
    public int  MetaId;
    public int  BlobOffset;
    public int  BlobLength;
}

// ─── String interner ────────────────────────────────────────────────────────

internal sealed class StringInterner
{
    private readonly Dictionary<string, int> _map = new();
    private readonly List<string> _list = [];

    public int Intern(string value)
    {
        if (_map.TryGetValue(value, out var id))
            return id;

        id = _list.Count;
        _list.Add(value);
        _map[value] = id;
        return id;
    }

    public string Get(int id) => _list[id];
    public int Count => _list.Count;
    public IReadOnlyList<string> All => _list;
}

// ─── Source location interner ───────────────────────────────────────────────

internal sealed class MetaInterner
{
    private readonly Dictionary<Meta, int> _map = new();
    private readonly List<Meta> _list = [];

    public int Intern(Meta loc)
    {
        if (_map.TryGetValue(loc, out var id))
            return id;

        id = _list.Count;
        _list.Add(loc);
        _map[loc] = id;
        return id;
    }

    public Meta Get(int id) => _list[id];
    public int Count => _list.Count;
    public IReadOnlyList<Meta> All => _list;
}

// ─── Per-type memory-mapped bucket ──────────────────────────────────────────

internal sealed class Bucket : IDisposable
{
    private const long DefaultRecordsCapacity = 64 * 1024 * 1024;  // 64 MB
    private const long DefaultBlobsCapacity   = 256 * 1024 * 1024; // 256 MB

    private readonly string _recordsPath;
    private readonly string _blobsPath;

    private MemoryMappedFile          _recordsMmf;
    private MemoryMappedViewAccessor  _recordsAccessor;
    private unsafe byte*              _recordsPtr;

    private MemoryMappedFile          _blobsMmf;
    private MemoryMappedViewAccessor  _blobsAccessor;
    private unsafe byte*              _blobsPtr;

    private long _recordsCapacity;
    private long _blobsCapacity;

    private int _recordCount;
    private int _blobOffset;

    private readonly Lock _writeLock = new();

    public int RecordCount => Volatile.Read(ref _recordCount);

    public Bucket(string directory, string typeName)
    {
        _recordsPath = Path.Combine(directory, $"{typeName}.records");
        _blobsPath   = Path.Combine(directory, $"{typeName}.blobs");

        _recordsCapacity = DefaultRecordsCapacity;
        _blobsCapacity   = DefaultBlobsCapacity;

        InitMmap();
    }

    private unsafe void InitMmap()
    {
        // Records file
        _recordsMmf = MemoryMappedFile.CreateFromFile(
            _recordsPath, FileMode.OpenOrCreate, null, _recordsCapacity);
        _recordsAccessor = _recordsMmf.CreateViewAccessor(0, _recordsCapacity);
        _recordsAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _recordsPtr);

        // Blobs file
        _blobsMmf = MemoryMappedFile.CreateFromFile(
            _blobsPath, FileMode.OpenOrCreate, null, _blobsCapacity);
        _blobsAccessor = _blobsMmf.CreateViewAccessor(0, _blobsCapacity);
        _blobsAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _blobsPtr);
    }

    public unsafe void Append(InternalRecord record, ReadOnlySpan<byte> blob)
    {
        lock (_writeLock)
        {
            var recordSize = Unsafe.SizeOf<InternalRecord>();
            var neededRecords = (long)(_recordCount + 1) * recordSize;
            var neededBlobs   = (long)_blobOffset + blob.Length;

            if (neededRecords > _recordsCapacity || neededBlobs > _blobsCapacity)
                Grow(neededRecords, neededBlobs);

            // Write blob
            record.BlobOffset = _blobOffset;
            record.BlobLength = blob.Length;
            blob.CopyTo(new Span<byte>(_blobsPtr + _blobOffset, blob.Length));
            _blobOffset += blob.Length;

            // Write record
            Unsafe.WriteUnaligned(_recordsPtr + (long)_recordCount * recordSize, record);

            // Publish — readers see the new count after the data is written
            Volatile.Write(ref _recordCount, _recordCount + 1);
        }
    }

    private unsafe void Grow(long neededRecords, long neededBlobs)
    {
        // Release current mappings
        _recordsAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _recordsAccessor.Dispose();
        _recordsMmf.Dispose();

        _blobsAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _blobsAccessor.Dispose();
        _blobsMmf.Dispose();

        // Double until sufficient
        while (_recordsCapacity < neededRecords) _recordsCapacity *= 2;
        while (_blobsCapacity < neededBlobs)     _blobsCapacity   *= 2;

        _recordsPtr = null;
        _blobsPtr   = null;
        InitMmap();
    }

    /// <summary>
    /// Returns the internal record at the given index.
    /// Safe to call concurrently with Append — only reads up to a snapshot of RecordCount.
    /// </summary>
    public unsafe InternalRecord GetRecord(int index)
    {
        var recordSize = Unsafe.SizeOf<InternalRecord>();
        return Unsafe.ReadUnaligned<InternalRecord>(_recordsPtr + (long)index * recordSize);
    }

    /// <summary>
    /// Returns a ReadOnlySpan over the blob data for a record.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetBlob(InternalRecord record)
    {
        return new ReadOnlySpan<byte>(_blobsPtr + record.BlobOffset, record.BlobLength);
    }

    /// <summary>
    /// Binary search for the first record with Timestamp >= value.
    /// </summary>
    public unsafe int LowerBound(long timestamp, int count)
    {
        var recordSize = Unsafe.SizeOf<InternalRecord>();
        int lo = 0, hi = count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            var ts = Unsafe.ReadUnaligned<InternalRecord>(_recordsPtr + (long)mid * recordSize).Timestamp;
            if (ts < timestamp) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>
    /// Binary search for the first record with Timestamp > value.
    /// </summary>
    public unsafe int UpperBound(long timestamp, int count)
    {
        var recordSize = Unsafe.SizeOf<InternalRecord>();
        int lo = 0, hi = count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            var ts = Unsafe.ReadUnaligned<InternalRecord>(_recordsPtr + (long)mid * recordSize).Timestamp;
            if (ts <= timestamp) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    public unsafe void Dispose()
    {
        _recordsAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _recordsAccessor.Dispose();
        _recordsMmf.Dispose();

        _blobsAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _blobsAccessor.Dispose();
        _blobsMmf.Dispose();
    }
}

// ─── Main database ──────────────────────────────────────────────────────────

public sealed class DebugDatabase : IDebugDb
{
    private readonly string _directory;
    private readonly StringInterner _moduleInterner = new();
    private readonly MetaInterner _sourceInterner = new();
    private readonly ConcurrentDictionary<Type, Bucket> _buckets = new();

    // Module name -> interned id, for fast query lookups
    private readonly ConcurrentDictionary<string, int> _moduleIdCache = new();

    public DebugDatabase(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    private Bucket GetOrCreateBucket<T>()
    {
        return _buckets.GetOrAdd(typeof(T), static (type, dir) =>
            new Bucket(dir, type.Name), _directory);
    }

    public void Append<T>(T entry) where T : IEntry
    {
        var bucket = GetOrCreateBucket<T>();

        // Intern source location + module (these are fast dictionary lookups for repeats)
        int sourceId;
        int moduleId;
        lock (_moduleInterner) // interners are not concurrent — low contention, cold path
        {
            moduleId = _moduleInterner.Intern(entry.Meta.Module);
            sourceId = _sourceInterner.Intern(entry.Meta);
        }
        _moduleIdCache.TryAdd(entry.Meta.Module, moduleId);

        // Serialize the entry payload via MemoryPack
        var blob = MemoryPackSerializer.Serialize(entry);

        var record = new InternalRecord
        {
            Timestamp        = entry.Timestamp.Nanoseconds,
            ModuleId         = moduleId,
            MetaId = sourceId,
            // BlobOffset and BlobLength are set by bucket.Append
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

            var blob = bucket.GetBlob(record);
            var entry = MemoryPackSerializer.Deserialize<T>(blob);
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
            var blob = bucket.GetBlob(record);
            var entry = MemoryPackSerializer.Deserialize<T>(blob);
            if (entry is not null)
                yield return entry;
        }
    }

    public Meta GetMeta(int id)
    {
        lock (_moduleInterner)
        {
            return _sourceInterner.Get(id);
        }
    }

    public void Dispose()
    {
        foreach (var bucket in _buckets.Values)
            bucket.Dispose();
    }
}
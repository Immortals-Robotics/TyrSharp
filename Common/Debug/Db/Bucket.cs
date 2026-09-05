using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tyr.Common.Debug.Db;

// ─── Per-shard memory-mapped bucket ─────────────────────────────────────────
//
// Two files per shard:
//   ShardName.records — header + flat array of InternalRecord, append-only
//   ShardName.blobs   — flat byte arena, append-only
//
// Record count and blob offset are stored in the first 8 bytes of the
// .records file as a header, so they survive restarts without external meta.
//
// Threading: one writer (Append, under _writeLock); readers take a BucketView
// snapshot and use only that for a whole query. Growth maps the files again
// at a larger capacity and publishes a new view; old views stay mapped until
// the bucket is disposed so a reader holding one is always safe. A stale
// view clamps the record count to what fits in its own mapping, so it never
// reads past its end even though the shared header may already be larger.

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct BucketHeader
{
    public int RecordCount;
    public int BlobOffset;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct InternalRecord
{
    public long Timestamp;
    public int BlobOffset;
    public int BlobLength;
}

internal sealed unsafe class BucketView
{
    private static readonly int HeaderSize = Unsafe.SizeOf<BucketHeader>();
    private static readonly int RecordSize = Unsafe.SizeOf<InternalRecord>();

    private readonly MemoryMappedFile _recordsMmf;
    private readonly MemoryMappedViewAccessor _recordsAccessor;
    private readonly MemoryMappedFile _blobsMmf;
    private readonly MemoryMappedViewAccessor _blobsAccessor;

    private readonly byte* _records;
    private readonly byte* _blobs;

    public long RecordsCapacity { get; }
    public long BlobsCapacity { get; }

    /// <summary>Records that fit in this mapping; a stale view clamps to this.</summary>
    public int MaxRecords { get; }

    public BucketView(string recordsPath, long recordsCapacity, string blobsPath, long blobsCapacity)
    {
        RecordsCapacity = recordsCapacity;
        BlobsCapacity = blobsCapacity;
        MaxRecords = (int)System.Math.Min(int.MaxValue, (recordsCapacity - HeaderSize) / RecordSize);

        _recordsMmf = MappedFiles.Open(recordsPath, recordsCapacity);
        _recordsAccessor = _recordsMmf.CreateViewAccessor(0, recordsCapacity);
        byte* rp = null;
        _recordsAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref rp);
        _records = rp;

        _blobsMmf = MappedFiles.Open(blobsPath, blobsCapacity);
        _blobsAccessor = _blobsMmf.CreateViewAccessor(0, blobsCapacity);
        byte* bp = null;
        _blobsAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref bp);
        _blobs = bp;
    }

    internal ref BucketHeader Header => ref Unsafe.AsRef<BucketHeader>(_records);

    /// <summary>Published record count, clamped to what this mapping can address.</summary>
    public int RecordCount
    {
        get
        {
            var count = Volatile.Read(ref Header.RecordCount);
            return count < MaxRecords ? count : MaxRecords;
        }
    }

    /// <summary>Raw header values; writer-side only.</summary>
    internal int RawRecordCount => Header.RecordCount;
    internal int RawBlobOffset => Header.BlobOffset;

    public InternalRecord GetRecord(int index)
    {
        return Unsafe.ReadUnaligned<InternalRecord>(_records + HeaderSize + (long)index * RecordSize);
    }

    public bool TryGetBlob(in InternalRecord record, out ReadOnlySpan<byte> blob)
    {
        if (record.BlobOffset < 0 || record.BlobLength < 0 ||
            (long)record.BlobOffset + record.BlobLength > BlobsCapacity)
        {
            blob = default;
            return false;
        }

        blob = new ReadOnlySpan<byte>(_blobs + record.BlobOffset, record.BlobLength);
        return true;
    }

    internal void WriteRecord(int index, in InternalRecord record)
    {
        Unsafe.WriteUnaligned(_records + HeaderSize + (long)index * RecordSize, record);
    }

    internal Span<byte> BlobSpan(int offset, int length)
    {
        return new Span<byte>(_blobs + offset, length);
    }

    /// <summary>First index in [0, count) whose timestamp is >= <paramref name="timestamp"/>.</summary>
    public int LowerBound(long timestamp, int count)
    {
        int lo = 0, hi = count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (GetRecord(mid).Timestamp < timestamp) lo = mid + 1;
            else hi = mid;
        }

        return lo;
    }

    /// <summary>First index in [0, count) whose timestamp is > <paramref name="timestamp"/>.</summary>
    public int UpperBound(long timestamp, int count)
    {
        int lo = 0, hi = count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (GetRecord(mid).Timestamp <= timestamp) lo = mid + 1;
            else hi = mid;
        }

        return lo;
    }

    public void Dispose()
    {
        _recordsAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _recordsAccessor.Dispose();
        _recordsMmf.Dispose();

        _blobsAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _blobsAccessor.Dispose();
        _blobsMmf.Dispose();
    }
}

internal sealed class Bucket : IDisposable
{
    // Capacity doubles on growth and every growth is two fresh file mappings, so start
    // large enough that a typical shard never grows during a session. Files are truncated
    // to their used size on Dispose, so the initial capacity costs no disk afterwards.
    private const long DefaultRecordsCapacity = 64 * 1024;   // ~4K records
    private const long DefaultBlobsCapacity   = 256 * 1024;
    private static readonly int HeaderSize = Unsafe.SizeOf<BucketHeader>();
    private static readonly int RecordSize = Unsafe.SizeOf<InternalRecord>();

    private readonly string _recordsPath;
    private readonly string _blobsPath;

    private BucketView _view;
    private readonly List<BucketView> _retired = [];
    private readonly Lock _writeLock = new();
    private bool _dirty;

    public Bucket(string directory, string shardName)
    {
        _recordsPath = Path.Combine(directory, $"{shardName}.records");
        _blobsPath   = Path.Combine(directory, $"{shardName}.blobs");

        var recordsCapacity = System.Math.Max(DefaultRecordsCapacity, MappedFiles.FileSize(_recordsPath));
        var blobsCapacity   = System.Math.Max(DefaultBlobsCapacity, MappedFiles.FileSize(_blobsPath));

        _view = new BucketView(_recordsPath, recordsCapacity, _blobsPath, blobsCapacity);

        // A file truncated on a previous Dispose is smaller than its content needs
        // only if the header is inconsistent; still, never trust it blindly.
        var neededRecords = HeaderSize + (long)_view.RawRecordCount * RecordSize;
        var neededBlobs = _view.RawBlobOffset;
        if (neededRecords > recordsCapacity || neededBlobs > blobsCapacity)
            Grow(neededRecords, neededBlobs);
    }

    /// <summary>Current mapping snapshot. Take it once per query and use only that.</summary>
    public BucketView View => Volatile.Read(ref _view);

    public void Append(long timestamp, ReadOnlySpan<byte> blob)
    {
        lock (_writeLock)
        {
            _dirty = true;
            var view = _view;
            var count = view.RawRecordCount;
            var blobOffset = view.RawBlobOffset;

            var neededRecords = HeaderSize + (long)(count + 1) * RecordSize;
            var neededBlobs   = (long)blobOffset + blob.Length;

            if (neededRecords > view.RecordsCapacity || neededBlobs > view.BlobsCapacity)
                view = Grow(neededRecords, neededBlobs);

            blob.CopyTo(view.BlobSpan(blobOffset, blob.Length));
            view.WriteRecord(count, new InternalRecord
            {
                Timestamp = timestamp,
                BlobOffset = blobOffset,
                BlobLength = blob.Length,
            });

            // Blob offset first, then count: the count is what readers key off.
            ref var header = ref view.Header;
            Volatile.Write(ref header.BlobOffset, blobOffset + blob.Length);
            Volatile.Write(ref header.RecordCount, count + 1);
        }
    }

    // Must be called under _writeLock (or from the constructor).
    private BucketView Grow(long neededRecords, long neededBlobs)
    {
        var old = _view;
        var recordsCapacity = old.RecordsCapacity;
        var blobsCapacity = old.BlobsCapacity;
        while (recordsCapacity < neededRecords) recordsCapacity *= 2;
        while (blobsCapacity < neededBlobs)     blobsCapacity   *= 2;

        // Readers may still hold the old view; keep it mapped until Dispose.
        _retired.Add(old);

        var grown = new BucketView(_recordsPath, recordsCapacity, _blobsPath, blobsCapacity);
        Volatile.Write(ref _view, grown);
        return grown;
    }

    public void Dispose()
    {
        var view = _view;
        var usedRecords = HeaderSize + (long)view.RawRecordCount * RecordSize;
        var usedBlobs = (long)view.RawBlobOffset;

        foreach (var retired in _retired)
            retired.Dispose();
        _retired.Clear();

        view.Dispose();

        // Only the instance that wrote shrinks the files: a second, read-only instance over
        // the same directory (playback of the live session) must not cut a mapping the
        // writer still holds.
        if (_dirty)
        {
            MappedFiles.TryTruncate(_recordsPath, usedRecords);
            MappedFiles.TryTruncate(_blobsPath, usedBlobs);
        }
    }
}

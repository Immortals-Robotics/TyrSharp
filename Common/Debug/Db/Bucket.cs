using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tyr.Common.Debug.Db;

// ─── Per-type memory-mapped bucket ──────────────────────────────────────────
//
// Two files per type:
//   TypeName.records  — flat array of InternalRecord, append-only
//   TypeName.blobs    — flat byte arena, append-only
//
// Record count and blob offset are stored in the first 8 bytes of the
// .records file as a header, so they survive restarts without external meta.

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct BucketHeader
{
    public int RecordCount;
    public int BlobOffset;
}

internal sealed class Bucket : IDisposable
{
    private const long DefaultRecordsCapacity = 64 * 1024 * 1024;  // 64 MB
    private const long DefaultBlobsCapacity   = 256 * 1024 * 1024; // 256 MB
    private static readonly int HeaderSize = Unsafe.SizeOf<BucketHeader>();
    private static readonly int RecordSize = Unsafe.SizeOf<InternalRecord>();

    private readonly string _recordsPath;
    private readonly string _blobsPath;

    private MemoryMappedFile          _recordsMmf = null!;
    private MemoryMappedViewAccessor  _recordsAccessor = null!;
    private unsafe byte*              _recordsPtr;

    private MemoryMappedFile          _blobsMmf = null!;
    private MemoryMappedViewAccessor  _blobsAccessor = null!;
    private unsafe byte*              _blobsPtr;

    private long _recordsCapacity;
    private long _blobsCapacity;

    private readonly Lock _writeLock = new();
    private readonly List<(MemoryMappedViewAccessor Accessor, MemoryMappedFile Mmf)> _retired = [];

    public unsafe int RecordCount => Volatile.Read(ref Unsafe.AsRef<int>(_recordsPtr));
    public unsafe int BlobOffset  => Volatile.Read(ref Unsafe.AsRef<int>(_recordsPtr + 4));

    public Bucket(string directory, string typeName)
    {
        _recordsPath = Path.Combine(directory, $"{typeName}.records");
        _blobsPath   = Path.Combine(directory, $"{typeName}.blobs");

        _recordsCapacity = System.Math.Max(DefaultRecordsCapacity, FileSize(_recordsPath));
        _blobsCapacity   = System.Math.Max(DefaultBlobsCapacity, FileSize(_blobsPath));

        InitMmap();

        // If files already existed, ensure capacity covers existing data
        var rc = RecordCount;
        var bo = BlobOffset;
        if (HeaderSize + (long)rc * RecordSize > _recordsCapacity || bo > _blobsCapacity)
        {
            Grow(HeaderSize + (long)rc * RecordSize, bo);
        }
    }

    private static long FileSize(string path) =>
        File.Exists(path) ? new FileInfo(path).Length : 0;

    private static MemoryMappedFile OpenMmf(string path, long capacity)
    {
        var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        return MemoryMappedFile.CreateFromFile(fs, null, capacity, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);
    }

    private unsafe void InitMmap()
    {
        _recordsMmf = OpenMmf(_recordsPath, _recordsCapacity);
        _recordsAccessor = _recordsMmf.CreateViewAccessor(0, _recordsCapacity);
        _recordsAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _recordsPtr);

        _blobsMmf = OpenMmf(_blobsPath, _blobsCapacity);
        _blobsAccessor = _blobsMmf.CreateViewAccessor(0, _blobsCapacity);
        _blobsAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _blobsPtr);
    }

    public unsafe void Append(InternalRecord record, ReadOnlySpan<byte> blob)
    {
        lock (_writeLock)
        {
            ref var header = ref Unsafe.AsRef<BucketHeader>(_recordsPtr);
            var count = header.RecordCount;
            var blobOff = header.BlobOffset;

            var neededRecords = HeaderSize + (long)(count + 1) * RecordSize;
            var neededBlobs   = (long)blobOff + blob.Length;

            if (neededRecords > _recordsCapacity || neededBlobs > _blobsCapacity)
                Grow(neededRecords, neededBlobs);

            // Re-read header after potential remap
            ref var h = ref Unsafe.AsRef<BucketHeader>(_recordsPtr);

            // Write blob
            record.BlobOffset = h.BlobOffset;
            record.BlobLength = blob.Length;
            blob.CopyTo(new Span<byte>(_blobsPtr + h.BlobOffset, blob.Length));

            // Write record (after header)
            Unsafe.WriteUnaligned(
                _recordsPtr + HeaderSize + (long)h.RecordCount * RecordSize,
                record);

            // Update header — blob offset first, then count (count is what readers see)
            Volatile.Write(ref h.BlobOffset, h.BlobOffset + blob.Length);
            Volatile.Write(ref h.RecordCount, h.RecordCount + 1);
        }
    }

    private unsafe void Grow(long neededRecords, long neededBlobs)
    {
        // Retire old mappings — readers may still hold pointers into them.
        // They will be disposed when the Bucket itself is disposed.
        _retired.Add((_recordsAccessor, _recordsMmf));
        _retired.Add((_blobsAccessor, _blobsMmf));

        while (_recordsCapacity < neededRecords) _recordsCapacity *= 2;
        while (_blobsCapacity < neededBlobs)     _blobsCapacity   *= 2;

        // Create new, larger mappings over the same files
        _recordsMmf = OpenMmf(_recordsPath, _recordsCapacity);
        _recordsAccessor = _recordsMmf.CreateViewAccessor(0, _recordsCapacity);
        byte* rp = null;
        _recordsAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref rp);
        _recordsPtr = rp;

        _blobsMmf = OpenMmf(_blobsPath, _blobsCapacity);
        _blobsAccessor = _blobsMmf.CreateViewAccessor(0, _blobsCapacity);
        byte* bp = null;
        _blobsAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref bp);
        _blobsPtr = bp;
    }

    public unsafe InternalRecord GetRecord(int index)
    {
        return Unsafe.ReadUnaligned<InternalRecord>(
            _recordsPtr + HeaderSize + (long)index * RecordSize);
    }

    public unsafe ReadOnlySpan<byte> GetBlob(InternalRecord record)
    {
        return new ReadOnlySpan<byte>(_blobsPtr + record.BlobOffset, record.BlobLength);
    }

    public unsafe int LowerBound(long timestamp, int count)
    {
        int lo = 0, hi = count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            var ts = Unsafe.ReadUnaligned<InternalRecord>(
                _recordsPtr + HeaderSize + (long)mid * RecordSize).Timestamp;
            if (ts < timestamp) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    public unsafe int UpperBound(long timestamp, int count)
    {
        int lo = 0, hi = count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            var ts = Unsafe.ReadUnaligned<InternalRecord>(
                _recordsPtr + HeaderSize + (long)mid * RecordSize).Timestamp;
            if (ts <= timestamp) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    public unsafe void Dispose()
    {
        foreach (var (accessor, mmf) in _retired)
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            accessor.Dispose();
            mmf.Dispose();
        }

        _recordsAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _recordsAccessor.Dispose();
        _recordsMmf.Dispose();

        _blobsAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _blobsAccessor.Dispose();
        _blobsMmf.Dispose();
    }
}

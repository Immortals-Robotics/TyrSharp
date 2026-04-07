using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tyr.Common.Debug.Db;

// ─── Mmap'd append-only frame storage ──────────────────────────────────────
//
// Single file: frames.data
//   [4 bytes: record count]
//   [InternalFrame][InternalFrame]...
//
// Each record is (Timestamp, ModuleId). No blobs needed.

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct InternalFrame
{
    public long Timestamp;
    public int  ModuleId;
}

internal sealed class FrameBucket : IDisposable
{
    private const long DefaultCapacity = 16 * 1024 * 1024; // 16 MB
    private const int HeaderSize = 4; // record count
    private static readonly int RecordSize = Unsafe.SizeOf<InternalFrame>();

    private readonly string _path;

    private MemoryMappedFile         _mmf;
    private MemoryMappedViewAccessor _accessor;
    private unsafe byte*             _ptr;

    private long _capacity;

    private readonly Lock _writeLock = new();
    private readonly List<(MemoryMappedViewAccessor Accessor, MemoryMappedFile Mmf)> _retired = [];

    public unsafe int RecordCount => Volatile.Read(ref Unsafe.AsRef<int>(_ptr));

    public FrameBucket(string directory)
    {
        _path = Path.Combine(directory, "frames.data");
        _capacity = System.Math.Max(DefaultCapacity, File.Exists(_path) ? new FileInfo(_path).Length : 0);

        InitMmap();

        var rc = RecordCount;
        if (HeaderSize + (long)rc * RecordSize > _capacity)
            Grow(HeaderSize + (long)rc * RecordSize);
    }

    private static MemoryMappedFile OpenMmf(string path, long capacity)
    {
        var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        return MemoryMappedFile.CreateFromFile(fs, null, capacity, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);
    }

    private unsafe void InitMmap()
    {
        _mmf = OpenMmf(_path, _capacity);
        _accessor = _mmf.CreateViewAccessor(0, _capacity);
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _ptr);
    }

    public unsafe void Append(InternalFrame frame)
    {
        lock (_writeLock)
        {
            var count = *(int*)_ptr;
            var needed = HeaderSize + (long)(count + 1) * RecordSize;

            if (needed > _capacity)
                Grow(needed);

            Unsafe.WriteUnaligned(_ptr + HeaderSize + (long)count * RecordSize, frame);
            Volatile.Write(ref *(int*)_ptr, count + 1);
        }
    }

    private unsafe void Grow(long needed)
    {
        _retired.Add((_accessor, _mmf));

        while (_capacity < needed) _capacity *= 2;

        _mmf = OpenMmf(_path, _capacity);
        _accessor = _mmf.CreateViewAccessor(0, _capacity);
        byte* p = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
        _ptr = p;
    }

    public unsafe InternalFrame GetRecord(int index)
    {
        return Unsafe.ReadUnaligned<InternalFrame>(_ptr + HeaderSize + (long)index * RecordSize);
    }

    public unsafe int LowerBound(long timestamp, int count)
    {
        int lo = 0, hi = count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            var ts = Unsafe.ReadUnaligned<InternalFrame>(
                _ptr + HeaderSize + (long)mid * RecordSize).Timestamp;
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
            var ts = Unsafe.ReadUnaligned<InternalFrame>(
                _ptr + HeaderSize + (long)mid * RecordSize).Timestamp;
            if (ts <= timestamp) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    public void Dispose()
    {
        foreach (var (accessor, mmf) in _retired)
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            accessor.Dispose();
            mmf.Dispose();
        }

        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();
    }
}

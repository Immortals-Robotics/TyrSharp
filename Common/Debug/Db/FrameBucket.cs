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
// Each record is (Timestamp, ModuleId). No blobs needed. Same view-snapshot
// scheme as Bucket: readers take a FrameView and use only that.

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct InternalFrame
{
    public long Timestamp;
    public int  ModuleId;
}

internal sealed unsafe class FrameView
{
    private const int HeaderSize = 4;
    private static readonly int RecordSize = Unsafe.SizeOf<InternalFrame>();

    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly byte* _ptr;

    public long Capacity { get; }
    public int MaxRecords { get; }

    public FrameView(string path, long capacity)
    {
        Capacity = capacity;
        MaxRecords = (int)System.Math.Min(int.MaxValue, (capacity - HeaderSize) / RecordSize);

        _mmf = MappedFiles.Open(path, capacity);
        _accessor = _mmf.CreateViewAccessor(0, capacity);
        byte* p = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
        _ptr = p;
    }

    internal ref int RawRecordCount => ref Unsafe.AsRef<int>(_ptr);

    public int RecordCount
    {
        get
        {
            var count = Volatile.Read(ref RawRecordCount);
            return count < MaxRecords ? count : MaxRecords;
        }
    }

    public InternalFrame GetRecord(int index)
    {
        return Unsafe.ReadUnaligned<InternalFrame>(_ptr + HeaderSize + (long)index * RecordSize);
    }

    internal void WriteRecord(int index, in InternalFrame frame)
    {
        Unsafe.WriteUnaligned(_ptr + HeaderSize + (long)index * RecordSize, frame);
    }

    public void Dispose()
    {
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();
    }
}

internal sealed class FrameBucket : IDisposable
{
    private const long DefaultCapacity = 1024 * 1024; // ~87K frames; doubles on growth, truncated on Dispose
    private const int HeaderSize = 4;
    private static readonly int RecordSize = Unsafe.SizeOf<InternalFrame>();

    private readonly string _path;
    private FrameView _view;
    private readonly List<FrameView> _retired = [];
    private readonly Lock _writeLock = new();
    private bool _dirty;

    public FrameBucket(string directory)
    {
        _path = Path.Combine(directory, "frames.data");
        var capacity = System.Math.Max(DefaultCapacity, MappedFiles.FileSize(_path));
        _view = new FrameView(_path, capacity);

        var needed = HeaderSize + (long)_view.RawRecordCount * RecordSize;
        if (needed > capacity)
            Grow(needed);
    }

    public FrameView View => Volatile.Read(ref _view);

    public void Append(InternalFrame frame)
    {
        lock (_writeLock)
        {
            _dirty = true;
            var view = _view;
            var count = view.RawRecordCount;
            var needed = HeaderSize + (long)(count + 1) * RecordSize;

            if (needed > view.Capacity)
                view = Grow(needed);

            view.WriteRecord(count, frame);
            Volatile.Write(ref view.RawRecordCount, count + 1);
        }
    }

    // Must be called under _writeLock (or from the constructor).
    private FrameView Grow(long needed)
    {
        var old = _view;
        var capacity = old.Capacity;
        while (capacity < needed) capacity *= 2;

        _retired.Add(old);

        var grown = new FrameView(_path, capacity);
        Volatile.Write(ref _view, grown);
        return grown;
    }

    public void Dispose()
    {
        var view = _view;
        var used = HeaderSize + (long)view.RawRecordCount * RecordSize;

        foreach (var retired in _retired)
            retired.Dispose();
        _retired.Clear();

        view.Dispose();

        if (_dirty)
            MappedFiles.TryTruncate(_path, used);
    }
}

using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tyr.Common.Debug.Db;

// ─── Interned source location as a fixed-size struct (all strings → int ids) ─

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct InternalSourceLocation
{
    public int ModuleId;
    public int LayerId;
    public int FileId;       // -1 = null
    public int MemberId;     // -1 = null
    public int Line;
    public int ExpressionId; // -1 = null
}

// ─── Mmap'd source location table ───────────────────────────────────────────
//
// File layout:
//   [4 bytes: count]
//   [InternalSourceLocation][InternalSourceLocation]...
//
// Deduplication lives in DebugDb (keyed by Meta.Id); this table only appends
// and resolves. Single writer (Intern) under the owner's lock; readers only
// touch rows below Count, which is published after the row is written.

internal sealed class MappedSourceLocationTable : IDisposable
{
    public const int MaxLocations = 64 * 1024;
    private const int HeaderSize = 4;
    private static readonly int EntrySize = Unsafe.SizeOf<InternalSourceLocation>();
    private static readonly long FileCapacity = HeaderSize + (long)MaxLocations * EntrySize;

    private readonly string _path;
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly unsafe byte* _ptr;
    private bool _dirty;

    public unsafe MappedSourceLocationTable(string path)
    {
        _path = path;
        _mmf = MappedFiles.Open(path, FileCapacity);
        _accessor = _mmf.CreateViewAccessor(0, FileCapacity);
        byte* p = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
        _ptr = p;
    }

    public unsafe int Count => Volatile.Read(ref Unsafe.AsRef<int>(_ptr));

    /// <summary>Append a source location and return its id. Single writer only.</summary>
    public unsafe int Intern(Meta loc, MappedStringPool strings)
    {
        var id = *(int*)_ptr;
        if (id >= MaxLocations)
            throw new InvalidOperationException($"Debug source location table is full ({MaxLocations} locations).");

        var isl = new InternalSourceLocation
        {
            ModuleId     = strings.Intern(loc.Module),
            LayerId      = strings.Intern(loc.Layer),
            FileId       = strings.Intern(loc.File),
            MemberId     = strings.Intern(loc.Member),
            Line         = loc.Line,
            ExpressionId = strings.Intern(loc.Expression),
        };

        _dirty = true;
        Unsafe.WriteUnaligned(_ptr + HeaderSize + (long)id * EntrySize, isl);
        Volatile.Write(ref Unsafe.AsRef<int>(_ptr), id + 1);
        return id;
    }

    public unsafe InternalSourceLocation GetInternal(int id)
    {
        return Unsafe.ReadUnaligned<InternalSourceLocation>(_ptr + HeaderSize + (long)id * EntrySize);
    }

    public Meta Get(int id, MappedStringPool strings)
    {
        var isl = GetInternal(id);
        return Meta.GetOrCreate(
            strings.Get(isl.ModuleId)!,
            strings.Get(isl.LayerId),
            strings.Get(isl.FileId),
            strings.Get(isl.MemberId),
            isl.Line,
            strings.Get(isl.ExpressionId));
    }

    public void Dispose()
    {
        var used = HeaderSize + (long)Count * EntrySize;

        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();

        if (_dirty)
            MappedFiles.TryTruncate(_path, used);
    }
}

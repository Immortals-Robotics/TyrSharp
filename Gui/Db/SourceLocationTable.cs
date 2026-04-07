using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tyr.Common.Debug;

namespace Tyr.Gui.Db;

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

internal sealed class MappedSourceLocationTable : IDisposable
{
    private const int MaxLocations = 64 * 1024;
    private const int HeaderSize = 4;
    private static readonly int EntrySize = Unsafe.SizeOf<InternalSourceLocation>();
    private static readonly long FileCapacity = HeaderSize + (long)MaxLocations * EntrySize;

    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly unsafe byte* _ptr;

    // In-memory reverse lookup: Meta → id
    // We rebuild Meta from string pool on load for the map keys
    private readonly Dictionary<Meta, int> _map = new();

    public unsafe MappedSourceLocationTable(string path)
    {
        var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        _mmf = MemoryMappedFile.CreateFromFile(fs, null, FileCapacity, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);
        _accessor = _mmf.CreateViewAccessor(0, FileCapacity);
        byte* p = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
        _ptr = p;
    }

    /// <summary>
    /// Rebuild the in-memory lookup map from the mmap'd data + string pool.
    /// Call after MappedStringPool.Reload().
    /// </summary>
    public unsafe void Reload(MappedStringPool strings)
    {
        _map.Clear();
        var count = *(int*)_ptr;
        var dataBase = _ptr + HeaderSize;

        for (int i = 0; i < count; i++)
        {
            var isl = Unsafe.ReadUnaligned<InternalSourceLocation>(dataBase + i * EntrySize);
            var loc = Resolve(isl, strings);
            _map[loc] = i;
        }
    }

    public unsafe int Count => *(int*)_ptr;

    /// <summary>
    /// Intern a source location. Returns its id.
    /// NOT thread-safe — caller must hold a lock.
    /// </summary>
    public unsafe int Intern(Meta loc, MappedStringPool strings)
    {
        if (_map.TryGetValue(loc, out var id)) return id;

        Assert.IsTrue(Count < MaxLocations);

        var isl = new InternalSourceLocation
        {
            ModuleId     = strings.Intern(loc.Module),
            LayerId      = strings.Intern(loc.Layer),
            FileId       = strings.Intern(loc.File),
            MemberId     = strings.Intern(loc.Member),
            Line         = loc.Line,
            ExpressionId = strings.Intern(loc.Expression),
        };

        id = *(int*)_ptr;

        // Write entry
        Unsafe.WriteUnaligned(_ptr + HeaderSize + id * EntrySize, isl);

        // Update count
        *(int*)_ptr = id + 1;

        _map[loc] = id;
        return id;
    }

    public unsafe InternalSourceLocation GetInternal(int id)
    {
        return Unsafe.ReadUnaligned<InternalSourceLocation>(_ptr + HeaderSize + id * EntrySize);
    }

    public Meta Get(int id, MappedStringPool strings)
    {
        return Resolve(GetInternal(id), strings);
    }

    private static Meta Resolve(InternalSourceLocation isl, MappedStringPool strings)
    {
        return new Meta
        {
            Module     = strings.Get(isl.ModuleId)!,
            Layer      = strings.Get(isl.LayerId)!,
            File       = strings.Get(isl.FileId),
            Member     = strings.Get(isl.MemberId),
            Line       = isl.Line,
            Expression = strings.Get(isl.ExpressionId),
        };
    }

    public void Dispose()
    {
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();
    }
}
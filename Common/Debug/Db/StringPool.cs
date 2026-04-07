using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Tyr.Common.Debug.Db;

// ─── Mmap'd append-only string pool ─────────────────────────────────────────
//
// File layout:
//   [4 bytes: string count]
//   [StringEntry][StringEntry]...   (fixed-size index)
//   ... gap to StringDataOffset ...
//   [utf8 bytes][utf8 bytes]...     (packed string data)
//
// We reserve the first region for the index and grow the string data from
// a fixed offset forward. Simple, no resizing needed for reasonable counts.

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct StringPoolEntry
{
    public int Offset; // into the data region
    public int Length; // byte length of UTF-8 data
}

internal sealed class MappedStringPool : IDisposable
{
    private const int MaxStrings = 64 * 1024;         // 64K unique strings is plenty
    private const int HeaderSize = 4;                  // string count
    private static readonly int IndexSize = MaxStrings * Unsafe.SizeOf<StringPoolEntry>();
    private const int StringDataOffset = HeaderSize + MaxStrings * 8; // 4 + 64K * 8 = ~512KB
    private const long FileCapacity = 16 * 1024 * 1024; // 16 MB total

    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly unsafe byte* _ptr;

    // In-memory index for fast lookup
    private readonly Dictionary<string, int> _map = new();
    private readonly List<string> _list = [];

    public unsafe MappedStringPool(string path)
    {
        var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        _mmf = MemoryMappedFile.CreateFromFile(fs, null, FileCapacity, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);
        _accessor = _mmf.CreateViewAccessor(0, FileCapacity);
        byte* p = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
        _ptr = p;

        // Load existing strings
        Reload();
    }

    private unsafe void Reload()
    {
        var count = *(int*)_ptr;
        var indexBase = _ptr + HeaderSize;

        for (int i = 0; i < count; i++)
        {
            var entry = Unsafe.ReadUnaligned<StringPoolEntry>(indexBase + i * Unsafe.SizeOf<StringPoolEntry>());
            var str = Encoding.UTF8.GetString(_ptr + entry.Offset, entry.Length);
            _list.Add(str);
            _map[str] = i;
        }
    }

    /// <summary>
    /// Intern a string. Returns its id. If null, returns -1.
    /// NOT thread-safe — caller must hold a lock.
    /// </summary>
    public unsafe int Intern(string? value)
    {
        if (value is null) return -1;
        if (_map.TryGetValue(value, out var id)) return id;

        id = _list.Count;
        Assert.IsTrue(id < MaxStrings);
        _list.Add(value);
        _map[value] = id;

        // Write UTF-8 data
        var utf8 = Encoding.UTF8.GetBytes(value);

        // Figure out where to write data: after the last string's data
        int dataOffset;
        if (id == 0)
        {
            dataOffset = StringDataOffset;
        }
        else
        {
            var prev = Unsafe.ReadUnaligned<StringPoolEntry>(
                _ptr + HeaderSize + (id - 1) * Unsafe.SizeOf<StringPoolEntry>());
            dataOffset = prev.Offset + prev.Length;
        }

        // Write string bytes
        utf8.AsSpan().CopyTo(new Span<byte>(_ptr + dataOffset, utf8.Length));

        // Write index entry
        var entry = new StringPoolEntry { Offset = dataOffset, Length = utf8.Length };
        Unsafe.WriteUnaligned(_ptr + HeaderSize + id * Unsafe.SizeOf<StringPoolEntry>(), entry);

        // Update count
        *(int*)_ptr = id + 1;

        return id;
    }

    public string? Get(int id) => id < 0 ? null : _list[id];
    public int Count => _list.Count;

    public void Dispose()
    {
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();
    }
}
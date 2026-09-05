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
//
// Threading: exactly one writer (Intern, under the owner's lock); any number of
// readers (Get / TryGetId) with no lock. The in-memory lookups are a
// ConcurrentDictionary and a copy-on-write array published after the element
// is written, so readers never observe a half-built entry.

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
    private static readonly int EntrySize = Unsafe.SizeOf<StringPoolEntry>();
    private static readonly int StringDataOffset = HeaderSize + MaxStrings * EntrySize; // ~512KB
    private const long FileCapacity = 16 * 1024 * 1024; // 16 MB total

    private readonly string _path;
    private MemoryMappedFile _mmf = null!;
    private MemoryMappedViewAccessor _accessor = null!;
    private unsafe byte* _ptr;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _map = new(StringComparer.Ordinal);
    private string[] _strings = new string[256];
    private int _count;
    private bool _dirty;

    public unsafe MappedStringPool(string path)
    {
        _path = path;
        var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        _mmf = MemoryMappedFile.CreateFromFile(fs, null, FileCapacity, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);
        _accessor = _mmf.CreateViewAccessor(0, FileCapacity);
        byte* p = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
        _ptr = p;

        Reload();
    }

    private unsafe void Reload()
    {
        var count = *(int*)_ptr;
        var indexBase = _ptr + HeaderSize;

        for (int i = 0; i < count; i++)
        {
            var entry = Unsafe.ReadUnaligned<StringPoolEntry>(indexBase + (long)i * EntrySize);
            var str = Encoding.UTF8.GetString(_ptr + entry.Offset, entry.Length);
            Publish(i, str);
        }
    }

    /// <summary>Store id → string so that readers see the element before the count.</summary>
    private void Publish(int id, string value)
    {
        var strings = _strings;
        if (id >= strings.Length)
        {
            var grown = new string[System.Math.Max(strings.Length * 2, id + 1)];
            Array.Copy(strings, grown, strings.Length);
            Volatile.Write(ref _strings, grown);
            strings = grown;
        }

        strings[id] = value;
        _map[value] = id;
        Volatile.Write(ref _count, id + 1);
    }

    /// <summary>
    /// Intern a string. Returns its id. If null, returns -1.
    /// Single writer only — caller must hold a lock.
    /// </summary>
    public unsafe int Intern(string? value)
    {
        if (value is null) return -1;
        if (_map.TryGetValue(value, out var id)) return id;

        id = _count;
        if (id >= MaxStrings)
            throw new InvalidOperationException($"Debug string pool is full ({MaxStrings} strings).");

        var utf8Length = Encoding.UTF8.GetByteCount(value);

        int dataOffset;
        if (id == 0)
        {
            dataOffset = StringDataOffset;
        }
        else
        {
            var prev = Unsafe.ReadUnaligned<StringPoolEntry>(_ptr + HeaderSize + (long)(id - 1) * EntrySize);
            dataOffset = prev.Offset + prev.Length;
        }

        if ((long)dataOffset + utf8Length > FileCapacity)
            throw new InvalidOperationException($"Debug string pool data region is full ({FileCapacity} bytes).");

        _dirty = true;
        Encoding.UTF8.GetBytes(value, new Span<byte>(_ptr + dataOffset, utf8Length));

        var entry = new StringPoolEntry { Offset = dataOffset, Length = utf8Length };
        Unsafe.WriteUnaligned(_ptr + HeaderSize + (long)id * EntrySize, entry);
        *(int*)_ptr = id + 1;

        Publish(id, value);
        return id;
    }

    public string? Get(int id)
    {
        if (id < 0 || id >= Volatile.Read(ref _count))
            return null;

        return Volatile.Read(ref _strings)[id];
    }

    public int Count => Volatile.Read(ref _count);

    public bool TryGetId(string? value, out int id)
    {
        if (value is null)
        {
            id = -1;
            return false;
        }

        return _map.TryGetValue(value, out id);
    }

    private unsafe long UsedBytes()
    {
        var count = *(int*)_ptr;
        if (count == 0)
            return HeaderSize;

        var last = Unsafe.ReadUnaligned<StringPoolEntry>(_ptr + HeaderSize + (long)(count - 1) * EntrySize);
        return last.Offset + last.Length;
    }

    public void Dispose()
    {
        var used = UsedBytes();

        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();

        if (_dirty)
            MappedFiles.TryTruncate(_path, used);
    }
}

internal static class MappedFiles
{
    public static MemoryMappedFile Open(string path, long capacity)
    {
        var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        return MemoryMappedFile.CreateFromFile(fs, null, capacity, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);
    }

    public static long FileSize(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    /// <summary>
    /// Shrink a file to its used length once every mapping over it is disposed. Mapping a file
    /// extends it to the mapping capacity, so without this every session carries the full
    /// power-of-two capacity of every shard on disk. Best effort: a failure just leaves the
    /// file at its mapped size.
    /// </summary>
    public static void TryTruncate(string path, long length)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            if (fs.Length > length)
                fs.SetLength(length);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

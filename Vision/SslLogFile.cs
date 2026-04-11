using System.Buffers.Binary;
using System.IO.Compression;
using Tyr.Common.Data.Ssl.Vision;
using Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Time;
using ProtoBuf;

namespace Tyr.Vision;

public sealed class SslLogFile : IDisposable
{
    public enum MessageType : int
    {
        Blank = 0,
        Unknown = 1,
        Vision2010 = 2,
        Refbox2013 = 3,
        Vision2014 = 4,
        Tracker2020 = 5,
        Index2021 = 6
    }

    public struct Entry
    {
        public Timestamp Timestamp;
        public long Offset;
        public int Size;
        public MessageType Type;
    }

    private readonly Stream _stream;
    private readonly List<Entry> _entries = new();
    private readonly string? _tempPath;
    
    public IReadOnlyList<Entry> Entries => _entries;
    public Timestamp StartTime => _entries.Count > 0 ? _entries[0].Timestamp : Timestamp.Zero;
    public Timestamp EndTime => _entries.Count > 0 ? _entries[^1].Timestamp : Timestamp.Zero;
    public DeltaTime Duration => EndTime - StartTime;

    public SslLogFile(string path)
    {
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            Log.ZLogInformation($"Decompressing GZip log to temporary file: {path}");
            _tempPath = Path.Combine(Path.GetTempPath(), $"tyr_log_{Guid.NewGuid()}.tmp");
            
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var gzip = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var tempFile = new FileStream(_tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                gzip.CopyTo(tempFile);
            }

            _stream = new FileStream(_tempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Log.ZLogTrace($"Decompressed size: {_stream.Length} bytes");
        }
        else
        {
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        IndexFile();
    }

    private void IndexFile()
    {
        _entries.Clear();
        if (_stream.Length < 16)
        {
            Log.ZLogWarning($"File too short to be an SSL log: {_stream.Length} bytes");
            return;
        }

        _stream.Position = 0;
        byte[] headerBuffer = new byte[12];
        _stream.ReadExactly(headerBuffer);
        string headerStr = System.Text.Encoding.ASCII.GetString(headerBuffer);

        if (headerStr != "SSL_LOG_FILE")
        {
            Log.ZLogWarning($"Unexpected header: {headerStr}. Attempting to parse from offset 0.");
            _stream.Position = 0;
        }
        else
        {
            _stream.Position = 16; // Skip header + version
        }

        byte[] meta = new byte[16];

        while (_stream.Position + 16 <= _stream.Length)
        {
            long offset = _stream.Position;
            if (_stream.Read(meta, 0, 16) < 16) break;

            // SSL Logs use Big-Endian (Java format)
            long ns = BinaryPrimitives.ReadInt64BigEndian(meta);
            int typeInt = BinaryPrimitives.ReadInt32BigEndian(meta.AsSpan(8));
            int size = BinaryPrimitives.ReadInt32BigEndian(meta.AsSpan(12));
            
            if (size < 0 || _stream.Position + size > _stream.Length)
            {
                Log.ZLogError($"Invalid packet size {size} at offset {offset}");
                break;
            }

            var type = (MessageType)typeInt;
            if (type != MessageType.Blank && type != MessageType.Index2021)
            {
                _entries.Add(new Entry 
                { 
                    Timestamp = Timestamp.FromNanoseconds(ns), 
                    Offset = _stream.Position, 
                    Size = size,
                    Type = type
                });
            }
            
            _stream.Position += size;
        }

        Log.ZLogTrace($"Indexed {_entries.Count} entries");
        if (_entries.Count > 0)
        {
            Log.ZLogTrace($"Start Time: {StartTime}");
            Log.ZLogTrace($"End Time: {EndTime}");
            Log.ZLogTrace($"Total Duration: {Duration}");
        }
    }

    public (MessageType Type, object Data)? ReadPacket(int index)
    {
        if (index < 0 || index >= _entries.Count) return null;
        var entry = _entries[index];
        _stream.Position = entry.Offset;
        byte[] buffer = new byte[entry.Size];
        _stream.ReadExactly(buffer);
        using var ms = new MemoryStream(buffer);

        try
        {
            switch (entry.Type)
            {
                case MessageType.Vision2010:
                case MessageType.Vision2014:
                    return (entry.Type, Serializer.Deserialize<WrapperPacket>(ms));
                case MessageType.Refbox2013:
                    return (entry.Type, Serializer.Deserialize<Referee>(ms));
                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Failed to deserialize packet of type {entry.Type} at index {index}");
            return null;
        }
    }

    public int FindIndex(Timestamp time)
    {
        if (_entries.Count == 0) return -1;
        if (time <= StartTime) return 0;
        if (time >= EndTime) return _entries.Count - 1;

        int low = 0;
        int high = _entries.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (_entries[mid].Timestamp == time) return mid;
            if (_entries[mid].Timestamp < time) low = mid + 1;
            else high = mid - 1;
        }
        return high; // Closest entry before time
    }

    public void Dispose()
    {
        _stream.Dispose();
        if (_tempPath != null && File.Exists(_tempPath))
        {
            try { File.Delete(_tempPath); } catch { /* ignore */ }
        }
    }
}

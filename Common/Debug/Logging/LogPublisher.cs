using System.Buffers;
using ZLogger;

namespace Tyr.Common.Debug.Logging;

public sealed class LogPublisher : IAsyncLogProcessor
{
    // Log text is open-ended (fps counters, timestamps), so the dedup cache is capped: once it fills,
    // repeated messages are still shared but new ones are plain allocations instead of permanent entries.
    private const int MaxCachedMessages = 4096;
    private static readonly InternedStringCache MessageCache = new(MaxCachedMessages);

    [ThreadStatic]
    private static ArrayBufferWriter<byte>? _buffer;

    public void Post(IZLoggerEntry log)
    {
        var info = log.LogInfo;

        var meta = Meta.GetOrCreate(info.Category.Name, null, info.FilePath, info.MemberName, info.LineNumber);
        var timestamp = Timestamp.FromDateTimeOffset(info.Timestamp.Utc);

        string message;
        if (log is IZLoggerFormattable formattable)
        {
            var buffer = _buffer ??= new ArrayBufferWriter<byte>(256);
            buffer.ResetWrittenCount();
            formattable.ToString(buffer);
            message = MessageCache.GetOrAdd(buffer.WrittenSpan);
        }
        else
        {
            message = log.ToString();
        }

        var entry = new Entry{Message = message, Level = info.LogLevel, Meta = meta, Timestamp = timestamp};

        DebugBus.Publish(entry);

        log.Return();
    }

    public ValueTask DisposeAsync()
    {
        return default;
    }
}

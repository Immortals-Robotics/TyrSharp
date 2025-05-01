using Tyr.Common.Dataflow;

namespace Tyr.Common.Debug.Logging;

public sealed class LogPublisher : IAsyncLogProcessor
{
    public void Post(IZLoggerEntry log)
    {
        var info = log.LogInfo;

        var meta = Meta.GetOrCreate(info.Category.Name, null, info.MemberName, info.FilePath, info.LineNumber);
        var timestamp = Timestamp.FromDateTimeOffset(info.Timestamp.Utc);
        var entry = new Entry(log.ToString(), info.LogLevel, meta, timestamp);

        Hub.Logs.Publish(entry);

        log.Return();
    }

    public ValueTask DisposeAsync()
    {
        return default;
    }
}
using Microsoft.Extensions.Logging;

namespace Tyr.Common.Debug.Logging;

public record Entry(
    string Message,
    LogLevel Level,
    Meta Meta,
    Timestamp Timestamp
) : IEntry
{
    public static Entry Empty => new(string.Empty, LogLevel.None, Meta.Empty, Timestamp.Now);

    public bool IsEmpty => Level == LogLevel.None;
}
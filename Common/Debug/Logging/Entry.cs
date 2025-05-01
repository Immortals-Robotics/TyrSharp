using Microsoft.Extensions.Logging;

namespace Tyr.Common.Debug.Logging;

public record Entry(
    string Message,
    LogLevel Level,
    Meta Meta,
    Timestamp Timestamp
);
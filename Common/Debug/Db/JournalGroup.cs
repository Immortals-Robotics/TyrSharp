using Tyr.Common.Debug.Logging;
using Tyr.Common.Time;

namespace Tyr.Common.Debug.Db;

/// <summary>
/// A journal entry that may represent multiple occurrences of the same log message
/// (same source location, message text, and level). Count is 1 when grouping is disabled.
/// </summary>
public record struct JournalGroup(Entry Entry, int Count, Timestamp LastTime)
{
    public Timestamp FirstTime => Entry.Timestamp;
}

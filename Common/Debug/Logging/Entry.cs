using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Tyr.Common.Debug.Logging
{
    [MemoryPackable]
    public partial record struct Entry : IEntry
    {
        public required string Message { get; init; }
        public LogLevel Level { get; init; }
        
        [MemoryPackIgnore]
        public Meta Meta { get; set; }

        public Time.Timestamp Timestamp { get; init; }

        [MemoryPackIgnore]
        public static Entry Empty => new Entry
        {
            Message = string.Empty,
            Level = LogLevel.None,
            Meta = Meta.Empty,
            Timestamp = Timestamp.Now
        };

        [MemoryPackIgnore]
        public bool IsEmpty => Level == LogLevel.None;
    }
}
using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Tyr.Common.Debug.Logging
{
    [MemoryPackable]
    public partial record struct Entry : IEntry
    {
        [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
        [MemoryPackIgnore] public Meta Meta { get; set; }
        
        public required string Message { get; init; }
        public LogLevel Level { get; init; }
    }
}

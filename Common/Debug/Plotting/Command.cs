using MemoryPack;

namespace Tyr.Common.Debug.Plotting;

[MemoryPackable]
public partial record struct Command : IEntry
{
    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey { get; init; }

    public required PlotValue Value { get; init; }
    public string? Title { get; init; }
}

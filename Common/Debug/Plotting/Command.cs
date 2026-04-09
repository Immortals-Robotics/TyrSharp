using MemoryPack;

namespace Tyr.Common.Debug.Plotting;

[MemoryPackable]
public partial record struct Command : IEntry
{
    public required PlotValue Value { get; init; }
    public string? Title { get; init; }

    [MemoryPackIgnore]
    public Meta Meta { get; set; }

    public Time.Timestamp Timestamp { get; init; }

    [MemoryPackIgnore]
    public string? ShardKey { get; init; }

    [MemoryPackIgnore]
    public static Command Empty => new()
    {
        Value = PlotValue.Empty,
        Title = null,
        Meta = Meta.Empty,
        ShardKey = string.Empty,
        Timestamp = Timestamp.Now,
    };

    [MemoryPackIgnore]
    public bool IsEmpty => Value.IsEmpty;
}

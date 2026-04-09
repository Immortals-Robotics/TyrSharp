using MemoryPack;

namespace Tyr.Common.Debug.Plotting;

[MemoryPackable]
public partial record struct Command : IEntry
{
    public required string Id { get; init; }
    public required PlotValue Value { get; init; }
    public string? Title { get; init; }

    [MemoryPackIgnore]
    public Meta Meta { get; set; }

    public Time.Timestamp Timestamp { get; init; }

    [MemoryPackIgnore]
    public static Command Empty => new()
    {
        Id = string.Empty,
        Value = PlotValue.Empty,
        Title = null,
        Meta = Meta.Empty,
        Timestamp = Timestamp.Now,
    };

    [MemoryPackIgnore]
    public bool IsEmpty => string.IsNullOrEmpty(Id);

    [MemoryPackIgnore]
    public string? ShardKey => Id;
}

using MemoryPack;
using Tyr.Common.Debug;
using Tyr.Common.Time;

namespace Tyr.Common.Vision.Data;

[MemoryPackable]
public partial record FilteredFrame : IEntry
{
    public long Id { get; init; }
    public Timestamp Timestamp { get; set; }
    public FilteredBall Ball { get; init; }
    public List<FilteredRobot> Robots { get; init; } = [];

    [MemoryPackIgnore] public Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey => null;
}
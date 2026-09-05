using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record struct LineSegment : IEntry
{
    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; } = Meta.Empty;
    [MemoryPackIgnore] public string? ShardKey => null;

    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }
    public Color Color { get; set; }
    public Options Options { get; set; }

    [MemoryPackConstructor]
    public LineSegment()
    {
    }
    
    public LineSegment(Math.Shapes.LineSegment segment)
    {
        Start = segment.Start;
        End = segment.End;
    }
}

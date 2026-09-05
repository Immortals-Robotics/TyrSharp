using System.Numerics;
using MemoryPack;
using Tyr.Common.Config;

namespace Tyr.Common.Debug.Drawing.Drawables;

[Configurable]
[MemoryPackable]
public partial record struct Arrow : IEntry
{
    [ConfigEntry] private static partial float DefaultHeadSize { get; set; } = 20f;

    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey => null;

    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }
    public float HeadSize { get; init; } = DefaultHeadSize;
    public Color Color { get; set; }
    public Options Options { get; set; }

    [MemoryPackConstructor]
    public Arrow()
    {
    }
    
    public Arrow(Math.Shapes.LineSegment segment)
    {
        Start = segment.Start;
        End = segment.End;
    }
}

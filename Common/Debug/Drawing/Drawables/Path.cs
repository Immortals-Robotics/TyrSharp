using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record struct Path : IEntry
{
    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; } = Meta.Empty;
    [MemoryPackIgnore] public string? ShardKey => null;

    public Vector2[] Points { get; init; }
    public Color Color { get; set; }
    public Options Options { get; set; }

    [MemoryPackConstructor]
    public Path()
    {
        Points = [];
    }
    
    public Path(IReadOnlyList<Vector2> points)
    {
        Points = points.ToArray();
    }
}

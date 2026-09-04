using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record struct Rectangle : IEntry
{
    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; } = Meta.Empty;
    [MemoryPackIgnore] public string? ShardKey => null;

    public Vector2 Min { get; init; }
    public Vector2 Max { get; init; }
    public Color Color { get; set; }
    public Options Options { get; set; }

    [MemoryPackConstructor]
    public Rectangle()
    {
    }
    
    public Rectangle(Math.Shapes.Rectangle rect)
    {
        Min = rect.Min;
        Max = rect.Max;
    }
}

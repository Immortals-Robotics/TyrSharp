using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record struct Circle : IEntry
{
    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey => null;

    public Vector2 Center { get; init; }
    public float Radius { get; init; }
    public Color Color { get; set; }
    public Options Options { get; set; }

    [MemoryPackConstructor]
    public Circle()
    {
    }
    
    public Circle(Math.Shapes.Circle circle)
    {
        Center = circle.Center;
        Radius = circle.Radius;
    }
}

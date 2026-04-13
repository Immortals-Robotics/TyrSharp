using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record struct Triangle : IEntry
{
    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey => null;

    public Vector2 A { get; init; }
    public Vector2 B { get; init; }
    public Vector2 C { get; init; }
    public Color Color { get; set; }
    public Options Options { get; set; }

    [MemoryPackConstructor]
    public Triangle()
    {
    }
    
    public Triangle(Math.Shapes.Triangle triangle)
    {
        A = triangle.Corner1;
        B = triangle.Corner2;
        C = triangle.Corner3;
    }
}

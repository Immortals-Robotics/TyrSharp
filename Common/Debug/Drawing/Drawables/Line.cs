using System.Numerics;
using MemoryPack;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record struct Line : IEntry
{
    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey => null;

    public Vector2 Point { get; init; }
    public Angle Angle { get; init; }
    public Color Color { get; set; }
    public Options Options { get; set; }

    [method: MemoryPackConstructor]    
    public Line()
    {
    }
    
    public Line(Math.Shapes.Line line)
    {
        Point = line.SomePoint;
        Angle = line.Angle;
    }
}

using System.Numerics;
using MemoryPack;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record struct Arc : IEntry
{
    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey => null;

    public Vector2 Center { get; init; }
    public float Radius { get; init; }
    
    public Angle Start { get; init; }
    public Angle End { get; init; }
    
    public bool Closed { get; init; }
    public Color Color { get; set; }
    public Options Options { get; set; }

    [MemoryPackConstructor]
    public Arc()
    {
    }

    public Arc(Data.Ssl.Vision.Geometry.FieldCircularArc arc)
    {
        Center = arc.Center;
        Radius = arc.Radius;
        Start = Angle.FromRad(arc.A1);
        End = Angle.FromRad(arc.A2);
        Closed = false;
    }
}

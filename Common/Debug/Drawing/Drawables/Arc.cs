using System.Numerics;
using MemoryPack;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record Arc : IDrawable
{
    public Vector2 Center { get; init; }
    public float Radius { get; init; }
    
    public Angle Start { get; init; }
    public Angle End { get; init; }
    
    public bool Closed { get; init; }

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
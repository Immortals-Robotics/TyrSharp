using System.Numerics;
using MemoryPack;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record Line : IDrawable
{
    public Vector2 Point { get; init; }
    public Angle Angle { get; init; }

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
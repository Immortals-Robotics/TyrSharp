using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record Circle : IDrawable
{
    public Vector2 Center { get; init; }
    public float Radius { get; init; }

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
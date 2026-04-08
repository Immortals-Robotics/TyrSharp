using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record Triangle : IDrawable
{
    public Vector2 A { get; init; }
    public Vector2 B { get; init; }
    public Vector2 C { get; init; }

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
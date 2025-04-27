using System.Numerics;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Triangle(Vector2 A, Vector2 B, Vector2 C) : IDrawable
{
    public Triangle(Math.Shapes.Triangle triangle)
        : this(triangle.Corner1, triangle.Corner2, triangle.Corner3)
    {
    }
}
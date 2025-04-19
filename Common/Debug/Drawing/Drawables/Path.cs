using System.Numerics;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Path : IDrawable
{
    public Path(IReadOnlyList<Vector2> points)
    {
        Points = points.ToArray();
    }

    public Vector2[] Points { get; init; }
}
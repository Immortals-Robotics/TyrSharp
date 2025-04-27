using System.Numerics;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Arrow(Vector2 Start, Vector2 End) : IDrawable
{
    public Arrow(Math.Shapes.LineSegment segment) : this(segment.Start, segment.End)
    {
    }
}
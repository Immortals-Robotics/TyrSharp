using System.Numerics;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct LineSegment(Vector2 Start, Vector2 End) : IDrawable
{
    public LineSegment(Math.Shapes.LineSegment segment) : this(segment.Start, segment.End)
    {
    }
}
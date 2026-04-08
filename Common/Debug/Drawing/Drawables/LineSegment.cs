using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
[method: MemoryPackConstructor]
public partial record LineSegment(Vector2 Start, Vector2 End) : IDrawable
{
    public LineSegment(Math.Shapes.LineSegment segment) : this(segment.Start, segment.End)
    {
    }
}
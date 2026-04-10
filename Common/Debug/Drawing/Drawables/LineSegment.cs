using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record LineSegment : IDrawable
{
    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }

    [MemoryPackConstructor]
    public LineSegment()
    {
    }
    
    public LineSegment(Math.Shapes.LineSegment segment)
    {
        Start = segment.Start;
        End = segment.End;
    }
}
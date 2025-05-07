using System.Numerics;
using Tyr.Common.Config;

namespace Tyr.Common.Debug.Drawing.Drawables;

[Configurable]
public readonly record struct Arrow : IDrawable
{
    [ConfigEntry] private static float DefaultHeadSize { get; set; } = 20f;

    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }
    public float HeadSize { get; init; }

    public Arrow(Vector2 Start, Vector2 End, float? headSize = null)
    {
        this.Start = Start;
        this.End = End;
        HeadSize = headSize ?? DefaultHeadSize;
    }

    public Arrow(Math.Shapes.LineSegment segment) : this(segment.Start, segment.End)
    {
    }
}
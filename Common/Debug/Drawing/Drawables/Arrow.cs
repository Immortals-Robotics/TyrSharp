using System.Numerics;
using MemoryPack;
using Tyr.Common.Config;

namespace Tyr.Common.Debug.Drawing.Drawables;

[Configurable]
[MemoryPackable]
public partial record Arrow : IDrawable
{
    [ConfigEntry] private static float DefaultHeadSize { get; set; } = 20f;

    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }
    public float HeadSize { get; init; } = DefaultHeadSize;

    [MemoryPackConstructor]
    public Arrow()
    {
    }
    
    public Arrow(Math.Shapes.LineSegment segment)
    {
        Start = segment.Start;
        End = segment.End;
    }
}
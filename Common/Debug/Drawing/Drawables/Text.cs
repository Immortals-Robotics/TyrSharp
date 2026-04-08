using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record Text : IDrawable
{
    public required string Content { get; init; }
    public Vector2 Position { get; init; }
    public float Size { get; init; } = 20;
    public TextAlignment Alignment { get; init; } = TextAlignment.Center;
}
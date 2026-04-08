using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record Rectangle : IDrawable
{
    public Vector2 Min { get; init; }
    public Vector2 Max { get; init; }

    [MemoryPackConstructor]
    public Rectangle()
    {
    }
    
    public Rectangle(Math.Shapes.Rectangle rect)
    {
        Min = rect.Min;
        Max = rect.Max;
    }
}
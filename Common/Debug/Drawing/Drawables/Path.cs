using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record Path : IDrawable
{
    public Vector2[] Points { get; init; }

    [MemoryPackConstructor]
    private Path()
    {
        Points = [];
    }
    
    public Path(IReadOnlyList<Vector2> points)
    {
        Points = points.ToArray();
    }
}
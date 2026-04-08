using System.Numerics;
using MemoryPack;
using Tyr.Common.Config;

namespace Tyr.Common.Debug.Drawing.Drawables;

[Configurable]
[MemoryPackable]
public partial record Point : IDrawable
{
    [ConfigEntry("Size of the cross used to draw points")]
    private static float DefaultSize { get; set; } = 25f;

    public Vector2 Position { get; init; }
    public float Size { get; init; } = DefaultSize;
}
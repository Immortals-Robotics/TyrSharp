using System.Numerics;
using Tyr.Common.Config;

namespace Tyr.Common.Debug.Drawing.Drawables;

[Configurable]
public readonly record struct Point : IDrawable
{
    [ConfigEntry("Size of the cross used to draw points")]
    private static float DefaultSize { get; set; } = 25f;

    public Vector2 Position { get; init; }
    public float Size { get; init; }

    public Point(Vector2 Position, float? Size = null)
    {
        this.Position = Position;
        this.Size = Size ?? DefaultSize;
    }
}
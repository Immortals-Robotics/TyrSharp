using System.Numerics;
using MemoryPack;
using Tyr.Common.Config;

namespace Tyr.Common.Debug.Drawing.Drawables;

[Configurable]
[MemoryPackable]
public partial record struct Point : IEntry
{
    [ConfigEntry("Size of the cross used to draw points")]
    private static partial float DefaultSize { get; set; } = 25f;

    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey => null;

    public Vector2 Position { get; init; }
    public float Size { get; init; } = DefaultSize;
    public Color Color { get; set; }
    public Options Options { get; set; }

    [MemoryPackConstructor]
    public Point()
    {
    }
}

using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Debug.Drawing.Drawables;

[MemoryPackable]
public partial record struct Text : IEntry
{
    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; } = Meta.Empty;
    [MemoryPackIgnore] public string? ShardKey => null;

    public required string Content { get; init; }
    public Vector2 Position { get; init; }
    public float Size { get; init; } = 20;
    public TextAlignment Alignment { get; init; } = TextAlignment.Center;
    public Color Color { get; set; }
    public Options Options { get; set; }

    [MemoryPackConstructor]
    public Text()
    {
    }
}

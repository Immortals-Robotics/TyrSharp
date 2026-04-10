using MemoryPack;

namespace Tyr.Common.Debug.Drawing;

[MemoryPackable]
public partial record struct Command : IEntry
{
    [MemoryPackIgnore] public Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey => null;
    
    public IDrawable Drawable { get; init; }
    public Color Color { get; init; }
    public Options Options { get; init; }
}

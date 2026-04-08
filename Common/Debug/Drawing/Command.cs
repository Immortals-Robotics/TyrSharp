using MemoryPack;

namespace Tyr.Common.Debug.Drawing;

[MemoryPackable]
public partial record struct Command : IEntry
{
    public IDrawable Drawable { get; init; }
    public Color Color { get; init; }
    public Options Options { get; init; }
    public Meta Meta { get; set; }
    public Time.Timestamp Timestamp { get; init; }
    
    public bool IsEmpty => Drawable is null;

    public static Command Empty => new()
    {
        Drawable = null!, Color = Color.Black,
        Options = new Options(), Meta = Meta.Empty, Timestamp = Timestamp.Now
    };
}
namespace Tyr.Common.Debug.Drawing;

public readonly record struct Command(
    IDrawable Drawable,
    Color Color,
    Options Options,
    Meta Meta,
    Timestamp Timestamp
) : IEntry
{
    public static Command Empty => new(null!, Color.Black, new Options(), Meta.Empty, Timestamp.Now);

    public bool IsEmpty => Drawable is null;
}
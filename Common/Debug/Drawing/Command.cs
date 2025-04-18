namespace Tyr.Common.Debug.Drawing;

public readonly record struct Command(
    IDrawable Drawable,
    Color Color,
    Options Options,
    string Category,
    DateTime Time,
    string? MemberName = null,
    string? FilePath = null,
    int LineNumber = 0
);
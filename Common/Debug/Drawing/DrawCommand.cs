namespace Tyr.Common.Debug.Drawing;

public readonly record struct DrawCommand(
    IDrawable Drawable,
    Color Color,
    DrawOptions Options,
    string Category,
    DateTime Time,
    string? MemberName = null,
    string? FilePath = null,
    int LineNumber = 0
);
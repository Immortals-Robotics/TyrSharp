namespace Tyr.Common.Debug.Drawing;

public readonly record struct Command(
    IDrawable Drawable,
    Color Color,
    Options Options,
    Meta Meta
);
using System.Numerics;

namespace Tyr.Gui.Rendering;

public readonly record struct Viewport(
    Vector2 Offset, // Top-left in screen space (e.g. from GetCursorScreenPos)
    Vector2 Size // Size of the region in pixels
);
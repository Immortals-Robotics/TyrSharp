using System.Numerics;

namespace Tyr.Gui.Rendering;

public record Viewport(
    Vector2 Offset, // Top-left in screen space (e.g. from GetCursorScreenPos)
    Vector2 Size // Size of the region in pixels
);
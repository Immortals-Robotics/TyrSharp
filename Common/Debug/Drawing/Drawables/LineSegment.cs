using System.Numerics;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct LineSegment(Vector2 Start, Vector2 End) : IDrawable;
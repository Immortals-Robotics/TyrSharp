using System.Numerics;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Triangle(Vector2 A, Vector2 B, Vector2 C) : IDrawable;
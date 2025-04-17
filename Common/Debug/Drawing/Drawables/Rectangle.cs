using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Rectangle(Vector2 Min, Vector2 Max) : IDrawable;
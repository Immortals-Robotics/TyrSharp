using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Circle(Vector2 Center, float Radius) : IDrawable;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Point(Vector2 Position) : IDrawable;
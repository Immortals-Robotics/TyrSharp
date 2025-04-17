using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Line(Vector2 Point, Angle Angle) : IDrawable;
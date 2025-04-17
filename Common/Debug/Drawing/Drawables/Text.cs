using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Text(string Content, Vector2 Position, float Size = 20f) : IDrawable;
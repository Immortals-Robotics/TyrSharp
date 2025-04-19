using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Robot(Vector2 Position, Angle? Orientation = null, int? Id = null) : IDrawable;
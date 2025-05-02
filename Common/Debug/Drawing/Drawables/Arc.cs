using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Arc(Vector2 Center, float Radius, Angle Start, Angle End) : IDrawable;
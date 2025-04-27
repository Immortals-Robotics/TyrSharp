using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Line(Vector2 Point, Angle Angle) : IDrawable
{
    public Line(Math.Shapes.Line line) : this(line.SomePoint, line.Angle)
    {
    }
}
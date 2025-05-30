﻿using System.Numerics;

namespace Tyr.Common.Debug.Drawing.Drawables;

public readonly record struct Rectangle(Vector2 Min, Vector2 Max) : IDrawable
{
    public Rectangle(Math.Shapes.Rectangle rect) : this(rect.Min, rect.Max)
    {
    }
}
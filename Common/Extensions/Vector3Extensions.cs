﻿using System.Numerics;

namespace Tyr.Common.Extensions;

public static class Vector3Extensions
{
    public static Vector2 Xy(this Vector3 v) => new(v.X, v.Y);
}
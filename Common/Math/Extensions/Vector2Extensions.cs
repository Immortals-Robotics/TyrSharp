using System.Numerics;

namespace Tyr.Common.Math.Extensions;

public static class Vector2Extensions
{
    public static Angle ToAngle(this Vector2 vector2) => Angle.FromVector(vector2);
}
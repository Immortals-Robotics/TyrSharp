using System.Numerics;

namespace Tyr.Common.Math.Shapes;

public readonly record struct Circle
{
    public Vector2 Center { get; init; }
    public float Radius { get; init; }

    public float Circumference => 2f * MathF.PI * Radius;

    public float Area => MathF.PI * Radius * Radius;

    public float Distance(Vector2 point) => Vector2.Distance(Center, point) - Radius;

    public bool Inside(Vector2 point, float margin = 0f) => Distance(point) <= margin;

    public Vector2 NearestOutside(Vector2 point, float margin = 0f)
    {
        var direction = point - Center;

        direction = Utils.ApproximatelyZero(direction.LengthSquared())
            ? Vector2.UnitX
            : Vector2.Normalize(direction);

        return Center + direction * (Radius + margin);
    }
}
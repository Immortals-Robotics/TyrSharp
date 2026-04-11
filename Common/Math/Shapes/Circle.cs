using System.Numerics;
using MemoryPack;

namespace Tyr.Common.Math.Shapes;

[MemoryPackable]
public partial record struct Circle : IObstacleShape
{
    public Vector2 Center { get; init; }
    public float Radius { get; init; }

    [MemoryPackIgnore]
    public float Circumference => 2f * MathF.PI * Radius;

    [MemoryPackIgnore]
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
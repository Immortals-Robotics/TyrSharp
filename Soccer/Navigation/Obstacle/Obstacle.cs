using System.Numerics;
using Tyr.Common.Math.Shapes;

namespace Tyr.Soccer.Navigation.Obstacle;

// Define Physicality enum with [Flags] for bitwise ops

public class Obstacle<T>(T shape, Physicality physicality)
    where T : IObstacleShape
{
    private readonly T _shape = shape;

    public bool Inside(Vector2 point, float margin = 0f) => _shape.Inside(point, margin);
    public float Distance(Vector2 point) => _shape.Distance(point);
    public Physicality Physicality { get; } = physicality;
}
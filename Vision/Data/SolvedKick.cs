using System.Numerics;

namespace Tyr.Vision.Data;

public class SolvedKick(Vector2 position, Vector3 velocity, Timestamp timestamp, Vector2 spin, string name)
{
    public Vector2 Position { get; init; } = position;
    public Vector3 Velocity { get; init; } = velocity;
    public Timestamp Timestamp { get; init; } = timestamp;
    public Vector2 Spin { get; init; } = spin;
    public string Name { get; init; } = name;
}
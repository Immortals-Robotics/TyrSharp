using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Common.Sender.Data;

public readonly struct Command
{
    public int VisionId { get; init; }

    public bool Halted { get; init; }
    public Vector2 Motion { get; init; }
    public Angle CurrentAngle { get; init; }
    public Angle TargetAngle { get; init; }
    public float Shoot { get; init; }
    public float Chip { get; init; }
    public float DribblerSpeed { get; init; }
    public float DribblerForce { get; init; }
}
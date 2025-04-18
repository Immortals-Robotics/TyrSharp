using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Data.Robot;

[ProtoContract]
public struct Command
{
    [ProtoMember(1)] public int VisionId;

    [ProtoMember(2)] public Vector2 MotionRaw;

    public System.Numerics.Vector2 Motion
    {
        get => MotionRaw;
        set => MotionRaw = value;
    }

    [ProtoMember(3)] public bool Halted;

    [ProtoMember(4)] public Angle CurrentAngle;
    [ProtoMember(5)] public Angle TargetAngle;

    [ProtoMember(6)] public float Shoot;
    [ProtoMember(7)] public float Chip;
    [ProtoMember(8)] public float Dribbler;
}
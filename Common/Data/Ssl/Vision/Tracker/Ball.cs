using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

[ProtoContract]
public struct Ball
{
    [ProtoMember(1, IsRequired = true)] public Vector3 PositionRaw { get; set; }

    public System.Numerics.Vector3 Position
    {
        get => PositionRaw;
        set => PositionRaw = value;
    }

    [ProtoMember(2)] public Vector3? VelocityRaw { get; set; }
    public System.Numerics.Vector3? Velocity => VelocityRaw;

    [ProtoMember(3)] public float? Visibility { get; set; }
}
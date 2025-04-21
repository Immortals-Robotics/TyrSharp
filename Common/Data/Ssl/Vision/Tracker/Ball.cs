using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

[ProtoContract]
public struct Ball
{
    [ProtoMember(1, IsRequired = true)] public Vector3 PositionProto { get; set; }

    public System.Numerics.Vector3 Position
    {
        get => PositionProto;
        set => PositionProto = value;
    }

    [ProtoMember(2)] public Vector3? VelocityProto { get; set; }
    public System.Numerics.Vector3? Velocity => VelocityProto;

    [ProtoMember(3)] public float? Visibility { get; set; }
}
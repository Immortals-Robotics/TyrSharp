using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

[ProtoContract]
public struct Robot
{
    [ProtoMember(1, IsRequired = true)] public RobotId Id { get; set; }

    [ProtoMember(2, IsRequired = true)] public Vector2 PositionProto { get; set; }

    public Vector2 Position
    {
        get => PositionProto;
        set => PositionProto = value;
    }

    [ProtoMember(3, IsRequired = true)] public float AngleRad { get; set; }

    public Angle Angle
    {
        get => Angle.FromRad(AngleRad);
        set => AngleRad = value.Rad;
    }

    [ProtoMember(4)] public Vector2? VelocityProto { get; set; }

    public System.Numerics.Vector2? Velocity
    {
        get => VelocityProto;
        set => VelocityProto = value;
    }

    [ProtoMember(5)] public float? AngularVelocityRad { get; set; }

    public Angle? AngularVelocity
    {
        get => AngularVelocityRad.HasValue ? Angle.FromRad(AngularVelocityRad.Value) : null;
        set => AngularVelocityRad = value?.Rad;
    }

    [ProtoMember(6)] public float? Visibility { get; set; }
}
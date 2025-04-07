using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

[ProtoContract]
public struct Robot
{
    [ProtoMember(1, IsRequired = true)] public RobotId Id { get; set; }

    [ProtoMember(2, IsRequired = true)] public Vector2 Position { get; set; }

    [ProtoMember(3, IsRequired = true)] public float AngleRad { get; set; }
    public Angle Angle => Angle.FromRad(AngleRad);

    [ProtoMember(4)] public Vector2? Velocity { get; set; }

    [ProtoMember(5)] public float? AngularVelocity { get; set; }

    [ProtoMember(6)] public float? Visibility { get; set; }
}
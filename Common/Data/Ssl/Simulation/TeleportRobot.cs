using System.Numerics;
using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public readonly struct TeleportRobot
{
    [ProtoMember(1, IsRequired = true)] public RobotId Id { get; init; }

    [ProtoMember(2)] public float? X { get; init; }
    [ProtoMember(3)] public float? Y { get; init; }
    
    public Vector2? Position
    {
        get => X.HasValue && Y.HasValue
            ? new Vector2(X.Value, Y.Value)
            : null;
        init
        {
            X = value?.X;
            Y = value?.Y;
        }
    }
    
    [ProtoMember(4)] public float? OrientationRad { get; init; }
    
    public Angle? Orientation
    {
        get => OrientationRad.HasValue ? Angle.FromRad(OrientationRad.Value) : null;
        init => OrientationRad = value?.Rad;
    }

    [ProtoMember(5)] public float? Vx { get; init; }
    [ProtoMember(6)] public float? Vy { get; init; }
    
    public Vector2? Velocity
    {
        get => Vx.HasValue && Vy.HasValue
            ? new Vector2(Vx.Value, Vy.Value)
            : null;
        init
        {
            Vx = value?.X;
            Vy = value?.Y;
        }
    }
    
    [ProtoMember(7)] public float? VAngularRad { get; init; }
    
    public Angle? VAngular
    {
        get => VAngularRad.HasValue ? Angle.FromRad(VAngularRad.Value) : null;
        init => VAngularRad = value?.Rad;
    }

    [ProtoMember(8)] public bool? Present { get; init; }
    [ProtoMember(9, IsRequired = false)] public bool ByForce { get; init; }
}
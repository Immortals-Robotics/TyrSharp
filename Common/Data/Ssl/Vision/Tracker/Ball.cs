using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

[ProtoContract]
public struct Ball
{
    [ProtoMember(1, IsRequired = true)] public Vector3 Position { get; set; }

    [ProtoMember(2)] public Vector3? Velocity { get; set; }

    [ProtoMember(3)] public float? Visibility { get; set; }
}
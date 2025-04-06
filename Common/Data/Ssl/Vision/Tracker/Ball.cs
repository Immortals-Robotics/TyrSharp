using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

[ProtoContract]
public struct Ball
{
    [ProtoMember(1, IsRequired = true)] public Vector3 Pos { get; set; }

    [ProtoMember(2)] public Vector3? Vel { get; set; }

    [ProtoMember(3)] public float? Visibility { get; set; }
}
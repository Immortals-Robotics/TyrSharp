using ProtoBuf;

namespace Tyr.Common.Ssl.Vision.Tracker;

[ProtoContract]
public class Ball
{
    [ProtoMember(1, IsRequired = true)] public Vector3 Pos { get; set; } = new();

    [ProtoMember(2)] public Vector3? Vel { get; set; }

    [ProtoMember(3)] public float? Visibility { get; set; }
}
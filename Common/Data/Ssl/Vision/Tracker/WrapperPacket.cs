using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

[ProtoContract]
public class WrapperPacket
{
    [ProtoMember(1, IsRequired = true)] public string Uuid { get; set; } = "";

    [ProtoMember(2)] public string? SourceName { get; set; }

    [ProtoMember(3)] public Frame? TrackedFrame { get; set; }
}
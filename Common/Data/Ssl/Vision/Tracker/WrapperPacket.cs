using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

/// <summary>
/// A wrapper packet containing meta data of the source.
/// Also serves for the possibility to extend the protocol later.
/// </summary>
[ProtoContract]
public class WrapperPacket
{
    /// <summary>
    /// A random UUID of the source that is kept constant at the source while running.
    /// If multiple sources are broadcasting to the same network, this id can be used to identify individual sources.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public string Uuid { get; set; } = "";

    /// <summary>
    /// The name of the source software that is producing this messages.
    /// </summary>
    [ProtoMember(2)] public string? SourceName { get; set; }

    /// <summary>
    /// The tracked frame containing all currently tracked objects.
    /// </summary>
    [ProtoMember(3)] public Frame? TrackedFrame { get; set; }
}
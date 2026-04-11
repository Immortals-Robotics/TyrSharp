using ProtoBuf;
using MemoryPack;

using Tyr.Common.Debug;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

/// <summary>
/// Contains models for ball motion prediction.
/// </summary>
[ProtoContract]
[MemoryPackable]
public partial struct BallModels : IEntry
{
    [MemoryPackIgnore] public Timestamp ExecutionTimestamp { get; set; }
    [MemoryPackIgnore] Timestamp IEntry.Timestamp { get => ExecutionTimestamp; set => ExecutionTimestamp = value; }
    [MemoryPackIgnore] public Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey => null;

    /// <summary>
    /// Two-Phase model for straight-kicked balls.
    /// </summary>
    [ProtoMember(1)] public BallModelStraightTwoPhase? StraightTwoPhase { get; set; }
    
    /// <summary>
    /// Fixed-Loss model for chipped balls.
    /// </summary>
    [ProtoMember(2)] public BallModelChipFixedLoss? ChipFixedLoss { get; set; }
}
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

/// <summary>
/// Contains models for ball motion prediction.
/// </summary>
[ProtoContract]
public struct BallModels
{
    /// <summary>
    /// Two-Phase model for straight-kicked balls.
    /// </summary>
    [ProtoMember(1)] public BallModelStraightTwoPhase? StraightTwoPhase { get; set; }
    
    /// <summary>
    /// Fixed-Loss model for chipped balls.
    /// </summary>
    [ProtoMember(2)] public BallModelChipFixedLoss? ChipFixedLoss { get; set; }
}
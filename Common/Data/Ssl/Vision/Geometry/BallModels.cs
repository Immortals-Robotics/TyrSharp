using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

[ProtoContract]
public struct BallModels
{
    [ProtoMember(1)] public BallModelStraightTwoPhase? StraightTwoPhase { get; set; }
    [ProtoMember(2)] public BallModelChipFixedLoss? ChipFixedLoss { get; set; }
}
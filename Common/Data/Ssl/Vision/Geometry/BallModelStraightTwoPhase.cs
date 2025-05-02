using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

/// <summary>
/// Two-Phase model for straight-kicked balls.
/// There are two phases with different accelerations during the ball kicks:
/// 1. Sliding
/// 2. Rolling
/// The full model is described in the TDP of ER-Force from 2016, which can be found here:
/// https://ssl.robocup.org/wp-content/uploads/2019/01/2016_ETDP_ER-Force.pdf
/// </summary>
[ProtoContract]
public struct BallModelStraightTwoPhase
{
    /// <summary>
    /// Ball sliding acceleration [m/s^2] (should be negative).
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public double AccSlide { get; set; }
    
    /// <summary>
    /// Ball rolling acceleration [m/s^2] (should be negative).
    /// </summary>
    [ProtoMember(2, IsRequired = true)] public double AccRoll { get; set; }
    
    /// <summary>
    /// Fraction of the initial velocity where the ball starts to roll.
    /// </summary>
    [ProtoMember(3, IsRequired = true)] public double KSwitch { get; set; }
}
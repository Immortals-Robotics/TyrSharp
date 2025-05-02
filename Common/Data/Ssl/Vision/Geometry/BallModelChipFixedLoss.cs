using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

/// <summary>
/// Fixed-Loss model for chipped balls.
/// Uses fixed damping factors for xy and z direction per hop.
/// </summary>
[ProtoContract]
public struct BallModelChipFixedLoss
{
    /// <summary>
    /// Chip kick velocity damping factor in XY direction for the first hop.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public double DampingXyFirstHop { get; set; }
    
    /// <summary>
    /// Chip kick velocity damping factor in XY direction for all following hops.
    /// </summary>
    [ProtoMember(2, IsRequired = true)] public double DampingXyOtherHops { get; set; }
    
    /// <summary>
    /// Chip kick velocity damping factor in Z direction for all hops.
    /// </summary>
    [ProtoMember(3, IsRequired = true)] public double DampingZ { get; set; }
}
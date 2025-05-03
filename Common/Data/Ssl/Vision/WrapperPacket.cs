using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision;

/// <summary>
/// Wrapper packet containing detection and geometry data from SSL-Vision.
/// </summary>
[ProtoContract]
public class WrapperPacket
{
    /// <summary>
    /// Detection frame containing information about detected balls and robots.
    /// Contains data such as:
    /// - Frame number (monotonously increasing)
    /// - Capture timestamp
    /// - Camera ID
    /// - Detected balls
    /// - Detected yellow and blue robots
    /// </summary>
    [ProtoMember(1)] public Detection.Frame? Detection { get; set; }
    
    /// <summary>
    /// Geometry data containing field dimensions and camera calibration.
    /// Contains:
    /// - Field size (dimensions, lines, arcs)
    /// - Camera calibration data
    /// - Ball and robot models
    /// </summary>
    [ProtoMember(2)] public Geometry.Data? Geometry { get; set; }
}
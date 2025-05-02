using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

/// <summary>
/// Contains geometry data for the SSL field, including field dimensions, camera calibration, and ball models.
/// </summary>
[ProtoContract]
public class Data
{
    /// <summary>
    /// Field size information including dimensions, lines, and arcs.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public FieldSize Field { get; set; }
    
    /// <summary>
    /// Camera calibration data for all cameras in the system.
    /// </summary>
    [ProtoMember(2)] public List<CameraCalibration> Calibrations { get; set; } = [];
    
    /// <summary>
    /// Ball motion models used for prediction.
    /// </summary>
    [ProtoMember(3)] public BallModels? BallModels { get; set; }
}
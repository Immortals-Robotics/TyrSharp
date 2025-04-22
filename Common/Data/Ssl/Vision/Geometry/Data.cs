using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

[ProtoContract]
public class Data
{
    [ProtoMember(1, IsRequired = true)] public FieldSize Field { get; set; }
    [ProtoMember(2)] public List<CameraCalibration> Calibrations { get; set; } = [];
    [ProtoMember(3)] public BallModels? BallModels { get; set; }
}
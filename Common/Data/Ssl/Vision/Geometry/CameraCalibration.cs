using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

[ProtoContract]
public struct CameraCalibration
{
    [ProtoMember(1, IsRequired = true)] public uint CameraId { get; set; }
    [ProtoMember(2, IsRequired = true)] public float FocalLength { get; set; }

    [ProtoMember(3, IsRequired = true)] public float PrincipalPointX { get; set; }
    [ProtoMember(4, IsRequired = true)] public float PrincipalPointY { get; set; }
    public Vector2 PrincipalPoint => new(PrincipalPointX, PrincipalPointY);

    [ProtoMember(5, IsRequired = true)] public float Distortion { get; set; }

    [ProtoMember(6, IsRequired = true)] public float Q0 { get; set; }
    [ProtoMember(7, IsRequired = true)] public float Q1 { get; set; }
    [ProtoMember(8, IsRequired = true)] public float Q2 { get; set; }
    [ProtoMember(9, IsRequired = true)] public float Q3 { get; set; }

    [ProtoMember(10, IsRequired = true)] public float Tx { get; set; }
    [ProtoMember(11, IsRequired = true)] public float Ty { get; set; }
    [ProtoMember(12, IsRequired = true)] public float Tz { get; set; }
    public Vector3 T => new(Tx, Ty, Tz);

    [ProtoMember(13)] public float? DerivedCameraWorldTx { get; set; }
    [ProtoMember(14)] public float? DerivedCameraWorldTy { get; set; }
    [ProtoMember(15)] public float? DerivedCameraWorldTz { get; set; }

    public Vector3 DerivedCameraWorld => new(
        DerivedCameraWorldTx.GetValueOrDefault(),
        DerivedCameraWorldTy.GetValueOrDefault(),
        DerivedCameraWorldTz.GetValueOrDefault());

    [ProtoMember(16)] public uint? PixelImageWidth { get; set; }
    [ProtoMember(17)] public uint? PixelImageHeight { get; set; }
}
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

/// <summary>
/// Contains calibration data for a camera in the SSL-Vision system.
/// </summary>
[ProtoContract]
public struct CameraCalibration
{
    /// <summary>
    /// Identifier of the camera.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public uint CameraId { get; set; }
    
    /// <summary>
    /// Focal length of the camera.
    /// </summary>
    [ProtoMember(2, IsRequired = true)] public float FocalLength { get; set; }

    /// <summary>
    /// X-coordinate of the principal point.
    /// </summary>
    [ProtoMember(3, IsRequired = true)] public float PrincipalPointX { get; set; }
    
    /// <summary>
    /// Y-coordinate of the principal point.
    /// </summary>
    [ProtoMember(4, IsRequired = true)] public float PrincipalPointY { get; set; }
    
    /// <summary>
    /// Vector representation of the principal point.
    /// </summary>
    public System.Numerics.Vector2 PrincipalPoint => new(PrincipalPointX, PrincipalPointY);

    /// <summary>
    /// Distortion parameter of the camera.
    /// </summary>
    [ProtoMember(5, IsRequired = true)] public float Distortion { get; set; }

    /// <summary>
    /// First component of the quaternion representing camera orientation.
    /// </summary>
    [ProtoMember(6, IsRequired = true)] public float Q0 { get; set; }
    
    /// <summary>
    /// Second component of the quaternion representing camera orientation.
    /// </summary>
    [ProtoMember(7, IsRequired = true)] public float Q1 { get; set; }
    
    /// <summary>
    /// Third component of the quaternion representing camera orientation.
    /// </summary>
    [ProtoMember(8, IsRequired = true)] public float Q2 { get; set; }
    
    /// <summary>
    /// Fourth component of the quaternion representing camera orientation.
    /// </summary>
    [ProtoMember(9, IsRequired = true)] public float Q3 { get; set; }

    /// <summary>
    /// X-coordinate of the camera translation.
    /// </summary>
    [ProtoMember(10, IsRequired = true)] public float Tx { get; set; }
    
    /// <summary>
    /// Y-coordinate of the camera translation.
    /// </summary>
    [ProtoMember(11, IsRequired = true)] public float Ty { get; set; }
    
    /// <summary>
    /// Z-coordinate of the camera translation.
    /// </summary>
    [ProtoMember(12, IsRequired = true)] public float Tz { get; set; }
    
    /// <summary>
    /// Vector representation of the camera translation.
    /// </summary>
    public System.Numerics.Vector3 T => new(Tx, Ty, Tz);

    /// <summary>
    /// Derived X-coordinate of the camera in world coordinates.
    /// </summary>
    [ProtoMember(13)] public float? DerivedCameraWorldTx { get; set; }
    
    /// <summary>
    /// Derived Y-coordinate of the camera in world coordinates.
    /// </summary>
    [ProtoMember(14)] public float? DerivedCameraWorldTy { get; set; }
    
    /// <summary>
    /// Derived Z-coordinate of the camera in world coordinates.
    /// </summary>
    [ProtoMember(15)] public float? DerivedCameraWorldTz { get; set; }

    /// <summary>
    /// Vector representation of the derived camera position in world coordinates.
    /// </summary>
    public System.Numerics.Vector3 DerivedCameraWorld => new(
        DerivedCameraWorldTx.GetValueOrDefault(),
        DerivedCameraWorldTy.GetValueOrDefault(),
        DerivedCameraWorldTz.GetValueOrDefault());

    /// <summary>
    /// Width of the camera image in pixels.
    /// </summary>
    [ProtoMember(16)] public uint? PixelImageWidth { get; set; }
    
    /// <summary>
    /// Height of the camera image in pixels.
    /// </summary>
    [ProtoMember(17)] public uint? PixelImageHeight { get; set; }
}
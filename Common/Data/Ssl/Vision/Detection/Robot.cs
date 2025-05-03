using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Detection;

/// <summary>
/// Represents a detected robot in the SSL-Vision system.
/// </summary>
[ProtoContract]
public struct Robot : IObject
{
    /// <summary>
    /// Confidence in [0-1] of the detection.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public float Confidence { get; set; }

    /// <summary>
    /// Id of the robot.
    /// </summary>
    [ProtoMember(2)] public uint? RobotId { get; set; }

    /// <summary>
    /// X-coordinate in [mm] in global ssl-vision coordinate system.
    /// </summary>
    [ProtoMember(3, IsRequired = true)] public float X { get; set; }
    
    /// <summary>
    /// Y-coordinate in [mm] in global ssl-vision coordinate system.
    /// </summary>
    [ProtoMember(4, IsRequired = true)] public float Y { get; set; }
    
    /// <summary>
    /// 2D position vector combining X and Y coordinates.
    /// </summary>
    public System.Numerics.Vector2 Position => new(X, Y);

    /// <summary>
    /// Orientation in [rad].
    /// </summary>
    [ProtoMember(5)] public float? OrientationRad { get; set; }
    
    /// <summary>
    /// Orientation as an angle object.
    /// </summary>
    public Math.Angle? Orientation => OrientationRad.HasValue ? Math.Angle.FromRad(OrientationRad.Value) : null;

    /// <summary>
    /// X-coordinate in [pixel] in the image.
    /// </summary>
    [ProtoMember(6, IsRequired = true)] public float PixelX { get; set; }
    
    /// <summary>
    /// Y-coordinate in [pixel] in the image.
    /// </summary>
    [ProtoMember(7, IsRequired = true)] public float PixelY { get; set; }
    
    /// <summary>
    /// 2D pixel position vector combining PixelX and PixelY coordinates.
    /// </summary>
    public System.Numerics.Vector2 PixelPosition => new(PixelX, PixelY);

    /// <summary>
    /// Height, as configured in ssl-vision for the respective team.
    /// </summary>
    [ProtoMember(8)] public float? Height { get; set; }
}
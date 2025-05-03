using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Detection;

/// <summary>
/// Represents a detected ball in the SSL-Vision system.
/// </summary>
[ProtoContract]
public struct Ball : IObject
{
    /// <summary>
    /// Confidence in [0-1] of the detection.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public float Confidence { get; set; }

    /// <summary>
    /// Area of the detected ball.
    /// </summary>
    [ProtoMember(2)] public uint? Area { get; set; }

    /// <summary>
    /// X-coordinate in [mm] in global ssl-vision coordinate system.
    /// </summary>
    [ProtoMember(3, IsRequired = true)] public float X { get; set; }
    
    /// <summary>
    /// Y-coordinate in [mm] in global ssl-vision coordinate system.
    /// </summary>
    [ProtoMember(4, IsRequired = true)] public float Y { get; set; }
    
    /// <summary>
    /// Z-coordinate in [mm] in global ssl-vision coordinate system.
    /// Not supported by ssl-vision, but might be set by simulators.
    /// </summary>
    [ProtoMember(5)] public float? Z { get; set; }

    // z is always 0 in the current ssl-vision
    /// <summary>
    /// 2D position vector combining X and Y coordinates.
    /// </summary>
    public System.Numerics.Vector2 Position => new(X, Y);

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
}
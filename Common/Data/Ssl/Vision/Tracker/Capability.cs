namespace Tyr.Common.Data.Ssl.Vision.Tracker;

/// <summary>
/// Capabilities that a source implementation can have.
/// </summary>
public enum Capability
{
    /// <summary>
    /// Unknown capability.
    /// </summary>
    Unknown = 0,
    
    /// <summary>
    /// The source can detect flying balls.
    /// </summary>
    DetectFlyingBalls = 1,
    
    /// <summary>
    /// The source can detect multiple balls simultaneously.
    /// </summary>
    DetectMultipleBalls = 2,
    
    /// <summary>
    /// The source can detect balls that have been kicked by robots.
    /// </summary>
    DetectKickedBalls = 3
}
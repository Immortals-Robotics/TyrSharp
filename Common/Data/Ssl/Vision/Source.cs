namespace Tyr.Common.Data.Ssl.Vision;

/// <summary>
/// Identifies the source that produced an SSL vision wrapper packet.
/// </summary>
public enum Source
{
    Unknown = 0,
    Other = 1,
    SslVision = 2,
    VisionProcessor = 3,
    GrSim = 4,
    ErForceSim = 5
}

using ProtoBuf;

namespace Tyr.Common.Ssl.Vision.Tracker;

[ProtoContract]
public enum Capability
{
    Unknown = 0,
    DetectFlyingBalls = 1,
    DetectMultipleBalls = 2,
    DetectKickedBalls = 3
}
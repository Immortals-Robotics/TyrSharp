using System.Numerics;

namespace Tyr.Vision.Data;

public interface IKickModelIdentification
{
    Vector3 KickVelocity { get; }
    Vector2 KickPosition { get; }
    Timestamp KickTimestamp { get; }
    int SampleAmount { get; }
}

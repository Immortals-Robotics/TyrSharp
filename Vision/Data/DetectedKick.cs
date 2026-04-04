using System.Numerics;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Util;

namespace Tyr.Vision.Data;

public class DetectedKick(
    RobotId robotId,
    RobotState robotState,
    Vector2 kickPosition,
    Timestamp timestamp,
    bool isFastDetection,
    List<MergedBall> ballStatesAfterKick)
{
    public RobotId RobotId { get; init; } = robotId;
    public RobotState RobotState { get; init; } = robotState;
    public Vector2 KickPosition { get; init; } = kickPosition;
    public Timestamp Timestamp { get; init; } = timestamp;
    public bool IsFastDetection { get; init; } = isFastDetection;
    public IReadOnlyList<MergedBall> BallStatesAfterKick { get; init; } = ballStatesAfterKick;


    public Vector2? GetKickDirection()
    {
        var samples = BallStatesAfterKick
            .OrderBy(ballState => ballState.LatestRawBall?.CaptureTimestamp ?? ballState.Timestamp)
            .Select(ballState => ballState.LatestRawBall?.Detection.Position ?? ballState.Position)
            .ToList();

        var res = BallHelpers.GetKickDirection(samples, 2);
        return res?.Item2;
    }
}

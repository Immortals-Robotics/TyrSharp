using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;

namespace Tyr.Vision.Util;

public class BallHelpers
{
    private FastKickDetector FastKickDetector { get; } = new();

    public DetectedKick? DetectKick(MergedBall? ball, List<FilteredRobot> robots)
    {
        if (ball == null) return null;
        var fastKick = FastKickDetector.Tick(ball.Value, robots);
        return fastKick;
    }

    public void Reset()
    {
        FastKickDetector.Reset();
    }
}
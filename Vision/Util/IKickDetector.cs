using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;

namespace Tyr.Vision.Util;

public interface IKickDetector
{
    DetectedKick? Tick(MergedBall ball, List<FilteredRobot> robots);

    void Reset();
}
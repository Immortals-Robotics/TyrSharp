using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;

namespace Tyr.Vision.Estimators;

public interface IKickEstimator : IDisposable
{
    void AddRawBall(RawBall record);

    KickFitResult? GetFitResult();

    bool IsDone(List<FilteredRobot> mergedRobots, Timestamp timestamp);

    KickEstimatorType Type { get; }
}

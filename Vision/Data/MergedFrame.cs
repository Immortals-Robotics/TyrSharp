using Tyr.Common.Vision.Data;

namespace Tyr.Vision.Data;

public class MergedFrame(MergedBall ball, List<FilteredRobot> robots)
{
    public MergedBall Ball { get; init; } = ball;
    public List<FilteredRobot> Robots = robots;
}
using System.Diagnostics;
using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;

namespace Tyr.Vision.Util;

[Configurable]
public partial class SlowKickDetector : IKickDetector
{
    [ConfigEntry(description: "Minimum time between two kicks [s]")]
    private static double MinDeltaTime { get; set; } = 0.1;

    private const int HistorySize = 5;

    private Timestamp _lastKickTs;
    private readonly List<MergedFrame> _frames = [];
    private readonly List<MergedBall> _balls = new(HistorySize);
    private readonly Dictionary<RobotId, List<FilteredRobot>> _robotsMap = [];
    private readonly List<List<FilteredRobot>> _listPool = [];

    private readonly KickValidator _kickValidator = new KickValidator();

    public DetectedKick? Tick(MergedBall ball, List<FilteredRobot> robots)
    {
        var rawBall = ball.LatestRawBall;
        if (!rawBall.HasValue) return null;

        if (rawBall.Value.Detection.Confidence <= 0.0) return null;

        var frame = new MergedFrame(ball, robots);
        _frames.Add(frame);

        if (_frames.Count > HistorySize)
        {
            _frames.RemoveAt(0);
        }

        if (_frames.Count < HistorySize) return null;
        return Detect();
    }

    private DetectedKick? Detect()
    {
        _balls.Clear();
        foreach (var f in _frames) _balls.Add(f.Ball);

        foreach (var list in _robotsMap.Values) { list.Clear(); _listPool.Add(list); }
        _robotsMap.Clear();

        foreach (var frame in _frames)
        {
            foreach (var robot in frame.Robots)
            {
                if (!_robotsMap.TryGetValue(robot.Id, out var list))
                {
                    if (_listPool.Count > 0)
                    {
                        list = _listPool[^1];
                        _listPool.RemoveAt(_listPool.Count - 1);
                    }
                    else
                    {
                        list = [];
                    }
                    _robotsMap[robot.Id] = list;
                }
                list.Add(robot);
            }
        }

        var firstBallPos = _balls[0].Position;
        List<FilteredRobot>? kickedBot = null;
        foreach (var robots in _robotsMap.Values)
        {
            if (robots.Count < HistorySize) continue; // no full history (new bot)
            if (Vector2.Distance(robots[0].State.Position, firstBallPos) > 1000) continue; // too far away
            if (!_kickValidator.Validate(_balls, robots)) continue;
            kickedBot = robots;
            break;
        }

        if (kickedBot != null && (Math.Abs((_balls[0].Timestamp - _lastKickTs).Seconds) > MinDeltaTime))
        {
            var robot = kickedBot[0];
            Log.ZLogDebug($"Kick detected, Bot: {robot.Id}");

            // Snapshot _balls since DetectedKick stores it by reference
            var ballSnapshot = _balls.ToList();
            var kick = new DetectedKick(
                robot.Id,
                robot.State,
                _balls[0].Position,
                _balls[0].Timestamp,
                false,
                ballSnapshot);
            _lastKickTs = _balls[0].Timestamp;

            var backtrack = _kickValidator.DirectionBacktrack(_balls, kickedBot);
            if (!backtrack.HasValue) return kick;
            Log.ZLogDebug($"Backtrack possible");
            kick = new DetectedKick(
                robot.Id,
                robot.State,
                backtrack.Value.Item2,
                backtrack.Value.Item1,
                false,
                ballSnapshot);
            _lastKickTs = backtrack.Value.Item1;

            return kick;
        }

        return null;
    }

    public void Reset()
    {
        _frames.Clear();
    }
}

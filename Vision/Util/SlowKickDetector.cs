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
        List<MergedBall> balls = _frames
            .Select(f => f.Ball)
            .ToList();

        Dictionary<RobotId, List<FilteredRobot>> robotsMap = _frames
            .SelectMany(f => f.Robots)
            .GroupBy(r => r.Id)
            .ToDictionary(g => g.Key, g => g.ToList());

        // remove bots from checklist if no full history is available (new bot)
        robotsMap = robotsMap.Where(e => e.Value.Count >= HistorySize).ToDictionary(e => e.Key, e => e.Value);

        // remove bots from checklist which are too far away
        robotsMap = robotsMap.Where(e => Vector2.Distance(e.Value[0].State.Position, balls[0].Position) <= 1000)
            .ToDictionary(e => e.Key, e => e.Value);


        var kickedBot = robotsMap.Values.FirstOrDefault(robots => _kickValidator.Validate(balls, robots));

        if ((kickedBot != null) && (Math.Abs((balls[0].Timestamp - _lastKickTs).Seconds) > MinDeltaTime))
        {
            var robot = kickedBot[0];
            Log.ZLogDebug($"Kick detected, Bot: {robot.Id}");

            var kick = new DetectedKick(
                robot.Id,
                robot.State,
                balls[0].Position,
                balls[0].Timestamp,
                false,
                balls);
            _lastKickTs = balls[0].Timestamp;

            var backtrack = _kickValidator.DirectionBacktrack(balls, kickedBot);
            if (!backtrack.HasValue) return kick;
            Log.ZLogDebug($"Backtrack possible");
            kick = new DetectedKick(
                robot.Id,
                robot.State,
                backtrack.Value.Item2,
                backtrack.Value.Item1,
                false,
                balls);
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
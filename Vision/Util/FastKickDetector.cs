using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;

namespace Tyr.Vision.Util;

[Configurable]
public partial class FastKickDetector : IKickDetector
{
    [ConfigEntry(description: "Minimum distance to ball for kick detection [mm]")]
    public static float KickNearBallMinDist { get; set; } = 150.0f;

    [ConfigEntry(description: "Ball velocity threshold [mm/s]")]
    private static double VelocityThreshold { get; set; } = 1000.0;

    [ConfigEntry(description: "Fast ball direction change threshold [deg]")]
    private static double DirectionThreshold { get; set; } = 20.0;

    [ConfigEntry(description: "Ball velocity threshold for direction change [mm/s]")]
    private static double VelocityThresholdDirection { get; set; } = 2000.0;


    private readonly Dictionary<uint, List<MergedBall>> _rawBallRecordsMap = new Dictionary<uint, List<MergedBall>>();


    public DetectedKick? Tick(MergedBall ball, List<FilteredRobot> robots)
    {
        Log.ZLogDebug($"Fast kick detector tick");
        var rawBall = ball.LatestRawBall;
        if (!rawBall.HasValue) return null;

        if (rawBall.Value.Detection.Confidence <= 0.0) return null;

        var cameraId = rawBall.Value.CameraId;

        _rawBallRecordsMap.TryAdd(cameraId, []);
        var rawBallRecords = _rawBallRecordsMap[cameraId];

        if (rawBallRecords.Count != 0)
        {
            if ((ball.Timestamp - rawBallRecords.Last().Timestamp).Seconds > 0.1)
            {
                rawBallRecords.Clear();
            }
        }

        rawBallRecords.Add(ball);

        if (rawBallRecords.Count > 3)
        {
            rawBallRecords.RemoveAt(0);
        }

        if (rawBallRecords.Count < 3) return null;

        if (!Detect(rawBallRecords, robots)) return null;

        var kickedBall = rawBallRecords[1];

        var kickingBot = robots
            .MinBy(robot => Vector2.Distance(robot.State.Position, kickedBall.Position));

        var allBalls = _rawBallRecordsMap.Values
            .SelectMany(list => list)
            .Where(b => b.Timestamp >= kickedBall.Timestamp)
            .ToList();

        return new DetectedKick(kickingBot.Id, kickingBot.State, kickedBall.Position, kickedBall.Timestamp, true,
            allBalls);
    }

    public void Reset()
    {
    }

    private bool Detect(List<MergedBall> rawBallsRecords, List<FilteredRobot> robots)
    {
        var ball0 = rawBallsRecords[0].LatestRawBall!.Value;
        var ball1 = rawBallsRecords[1].LatestRawBall!.Value;
        var ball2 = rawBallsRecords[2].LatestRawBall!.Value;

        var nearRobotsCount = robots
            .Count(robot => Vector2.Distance(robot.State.Position, ball0.Detection.Position) < KickNearBallMinDist);

        if (nearRobotsCount == 0) return false;

        var t0 = ball0.CaptureTimestamp.Seconds;
        var t1 = ball1.CaptureTimestamp.Seconds;
        var t2 = ball2.CaptureTimestamp.Seconds;

        var dt1 = t1 - t0;
        var dt2 = t2 - t1;

        var vel1 = (ball1.Detection.Position - ball0.Detection.Position) * (float)(1.0 / dt1);
        var vel2 = (ball2.Detection.Position - ball1.Detection.Position) * (float)(1.0 / dt2);
        Log.ZLogCritical($"Vel1: {vel1.Length()}, Vel2: {vel2.Length()}");

        if ((vel1.Length() > 10000) || (vel2.Length() > 10000))
        {
            return false;
        }

        // trigger on velocity jump
        if ((vel2.Length() - vel1.Length()) > VelocityThreshold)
        {
            return true;
        }

        // trigger on large vel AND direction jump
        return (vel1.Length() > VelocityThresholdDirection) && (vel2.Length() >
                                                                VelocityThresholdDirection) &&
               (Math.Abs(vel1.AngleDiff(vel2).DegNormalized) > DirectionThreshold)
            ;
    }
}
using Tyr.Common.Config;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Math;
using Tyr.Common.Time;
using DetectionFrame = Tyr.Common.Data.Ssl.Vision.Detection.Frame;
using RawBall = Tyr.Vision.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;

namespace Tyr.Vision;

[Configurable]
public class Camera(uint id)
{
    [ConfigEntry("Time in [s] after an invisible ball is removed")]
    private static float InvisibleLifetimeBall { get; set; } = 1f;

    [ConfigEntry("Time in [s] after an invisible robot is removed")]
    private static float InvisibleLifetimeRobot { get; set; } = 2f;

    [ConfigEntry("Maximum number of ball trackers")]
    private static int MaxBallTrackers { get; set; } = 10;

    [ConfigEntry("Max. distance to copy state from filtered bot to new trackers")]
    private static float CopyTrackerMaxDistance { get; set; } = 200f;

    public uint Id { get; } = id;

    public uint FrameId { get; private set; }
    public DateTime Time { get; private set; }
    public DateTime LastBallOnCamTime { get; private set; }

    public float DeltaTime => _frameTimeEstimator.Estimate?.Slope ?? 0f;

    private readonly Dictionary<RobotId, Tracker.Robot> _robots = [];
    private readonly List<Tracker.Ball> _ballTrackers = [];

    private const int FrameEstimatorHistoryCount = 100;
    private const int FrameEstimatorStride = 10;
    private readonly LineEstimator _frameTimeEstimator = new(FrameEstimatorHistoryCount);

    private CameraCalibration? _calibration;
    private FieldSize? _fieldSize;

    public void OnCalibration(CameraCalibration calibration)
    {
        _calibration = calibration;
    }

    public void OnFieldSize(FieldSize fieldSize)
    {
        _fieldSize = fieldSize;
    }

    public void OnFrame(DetectionFrame frame)
    {
        // frame id
        var expectedFrameId = FrameId == 0 ? frame.FrameNumber : FrameId + 1;
        if (frame.FrameNumber != expectedFrameId)
        {
            Logger.ZLogWarning($"Camera {Id} frame id mismatch, expected {expectedFrameId}, got {frame.FrameNumber}");

            if (Math.Abs((int)expectedFrameId - (int)frame.FrameNumber) > 10)
                Reset();
        }

        FrameId = frame.FrameNumber;

        // time
        Time = frame.CaptureTime;

        if (!_frameTimeEstimator.IsFull || frame.FrameNumber % FrameEstimatorStride == 0)
        {
            _frameTimeEstimator.AddSample(frame.FrameNumber, frame.CaptureTime.ToUnixTimeSeconds());
        }

        Logger.ZLogTrace($"Camera {Id} Delta time: {DeltaTime}");

        // detections
        ProcessRobots(frame);
        ProcessBalls(frame);
    }

    private void Reset()
    {
        _frameTimeEstimator.Reset();
        _robots.Clear();
        _ballTrackers.Clear();
    }

    private void ProcessRobots(DetectionFrame frame)
    {
        // TODO: process robots
    }

    private void ProcessBalls(DetectionFrame frame)
    {
        // remove trackers of balls that have not been visible or were out of the field for too long
        _ballTrackers.RemoveAll(tracker =>
            (frame.CaptureTime - tracker.LastUpDateTime).TotalSeconds > InvisibleLifetimeBall ||
            (frame.CaptureTime - tracker.LastInFieldTime).TotalSeconds > InvisibleLifetimeBall);

        // do a prediction on all trackers
        foreach (var ball in _ballTrackers) ball.Predict(frame.CaptureTime);

        if (frame.Balls.Count > 0) LastBallOnCamTime = Time;

        // iterate over all balls on the camera
        foreach (var rawBall in frame.Balls.Select(detection => new RawBall(detection, frame)))
        {
            var consumed = _ballTrackers.Any(t => t.Update(rawBall, _fieldSize?.FieldRect));
            if (consumed) continue;

            // This is a new ball, we need to create a new tracker for it
            Logger.ZLogTrace($"Creating a new tracker for {rawBall} at {rawBall.Detection.Position}");

            // Skip if we already have too many trackers
            if (_ballTrackers.Count > MaxBallTrackers)
            {
                Logger.ZLogWarning($"Skipping {rawBall} as we already have {_ballTrackers.Count} trackers");
                continue;
            }

            // Skip the balls that are outside the field
            if (_fieldSize != null &&
                !_fieldSize.FieldRectWithBoundary.Inside(rawBall.Detection.Position)) continue;

            var tracker = new Tracker.Ball(rawBall)
            {
                MaxDistance = 500
            };

            _ballTrackers.Add(tracker);
        }
    }
}
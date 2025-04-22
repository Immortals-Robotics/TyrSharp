using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math;
using Tyr.Common.Time;

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
    public Timestamp Timestamp { get; private set; }
    public Timestamp LastBallOnCamTimestamp { get; private set; }

    // average delta time in seconds
    public DeltaTime DeltaTime => DeltaTime.FromSeconds(_frameTimeEstimator.Estimate?.Slope ?? 0f);
    public float Fps => (float)(1 / DeltaTime.Seconds);

    private readonly Dictionary<RobotId, Tracker.Robot> _robots = [];
    private readonly List<Tracker.Ball> _ballTrackers = [];

    private const int FrameEstimatorHistoryCount = 100;
    private const int FrameEstimatorStride = 10;
    private readonly LineEstimator _frameTimeEstimator = new(FrameEstimatorHistoryCount);

    private CameraCalibration? _calibration;
    private FieldSize? _fieldSize;

    private float RobotRadius => _fieldSize?.MaxRobotRadius ?? 90f;

    public void OnCalibration(CameraCalibration calibration)
    {
        _calibration = calibration;
    }

    public void OnFieldSize(FieldSize fieldSize)
    {
        _fieldSize = fieldSize;
    }

    public void OnFrame(DetectionFrame frame, FilteredFrame lastFilteredFrame)
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
        Timestamp = frame.CaptureTime;

        if (!_frameTimeEstimator.IsFull || frame.FrameNumber % FrameEstimatorStride == 0)
        {
            _frameTimeEstimator.AddSample(frame.FrameNumber, frame.CaptureTime.Seconds);
        }

        Logger.ZLogTrace($"Camera {Id} FPS: {Fps:F2}");

        // detections
        ProcessRobots(frame, lastFilteredFrame.Robots);
        ProcessBalls(frame, lastFilteredFrame.Ball);
    }

    private void Reset()
    {
        _frameTimeEstimator.Reset();
        _robots.Clear();
        _ballTrackers.Clear();
    }

    private bool IsOutside(Vector2 position)
    {
        return _fieldSize.HasValue && !_fieldSize.Value.FieldRectangleWithBoundary.Inside(position);
    }

    private void ProcessRobots(DetectionFrame frame, IReadOnlyList<FilteredRobot> filteredRobots)
    {
        // Remove trackers that are either too old or outside the field
        // can't directly remove from the dictionary while iterating over it, so we do it in two steps
        _robots.RemoveAll((_, tracker) =>
        {
            var tooOld = frame.CaptureTime - tracker.LastUpdateTimestamp >
                         DeltaTime.FromSeconds(InvisibleLifetimeRobot);

            var outsideField = IsOutside(tracker.GetPosition(frame.CaptureTime));

            return tooOld || outsideField;
        });

        // do a prediction on all trackers
        foreach (var r in _robots.Values)
        {
            r.Predict(frame.CaptureTime, DeltaTime);
        }

        foreach (var detection in frame.RobotsBlue)
        {
            var raw = new RawRobot(detection, frame);
            ProcessRobot(raw, TeamColor.Blue, filteredRobots);
        }

        foreach (var detection in frame.RobotsYellow)
        {
            var raw = new RawRobot(detection, frame);
            ProcessRobot(raw, TeamColor.Yellow, filteredRobots);
        }
    }

    private void ProcessRobot(RawRobot robot, TeamColor color, IReadOnlyList<FilteredRobot> filteredRobots)
    {
        var id = new RobotId() { Id = robot.Detection.RobotId, Team = color };

        // check if there are other robots very close by, could be a false vision detection then
        // we filter out the robot with the cam bots id before to allow trackers at the same location
        var shouldIgnore = filteredRobots.Any(filteredRobot =>
            filteredRobot.Id == id &&
            Vector2.Distance(filteredRobot.Position, robot.Detection.Position) < RobotRadius * 1.5);

        if (shouldIgnore)
        {
            Logger.ZLogDebug($"Ignoring robot {id} on camera {Id}");
        }
        else
        {
            if (_robots.TryGetValue(id, out var tracker))
            {
                // we already have a tracker for that robot, update it
                tracker.Update(robot);
            }
            else
            {
                // completely new robot on the field
                FilteredRobot? filteredRobot = filteredRobots.FirstOrDefault(filtered =>
                {
                    if (filtered.Id != id) return false;

                    var inside = !IsOutside(filtered.Position);
                    var close = Vector2.Distance(filtered.Position, robot.Detection.Position) < CopyTrackerMaxDistance;

                    return inside && close;
                });

                tracker = filteredRobot != null
                    ? new Tracker.Robot(robot, filteredRobot.Value, color) // on a different camera already 
                    : new Tracker.Robot(robot, color); // completely new robot on the field


                _robots.Add(id, tracker);
            }
        }
    }

    private void ProcessBalls(DetectionFrame frame, FilteredBall? lastFilteredBall)
    {
        // remove trackers of balls that have not been visible or were out of the field for too long
        _ballTrackers.RemoveAll(tracker =>
            frame.CaptureTime - tracker.LastUpdateTimestamp > DeltaTime.FromSeconds(InvisibleLifetimeBall) ||
            frame.CaptureTime - tracker.LastInFieldTimestamp > DeltaTime.FromSeconds(InvisibleLifetimeBall));

        // do a prediction on all trackers
        foreach (var ball in _ballTrackers) ball.Predict(frame.CaptureTime);

        if (frame.Balls.Count > 0) LastBallOnCamTimestamp = Timestamp;

        // iterate over all balls on the camera
        foreach (var detection in frame.Balls)
        {
            var raw = new RawBall(detection, frame);

            var consumed = _ballTrackers.Any(t => t.Update(raw, _fieldSize?.FieldRectangle));
            if (consumed) continue;

            // This is a new ball, we need to create a new tracker for it
            Logger.ZLogTrace($"Creating a new tracker for {raw} at {raw.Detection.Position}");

            // Skip if we already have too many trackers
            if (_ballTrackers.Count > MaxBallTrackers)
            {
                Logger.ZLogWarning($"Skipping {raw} as we already have {_ballTrackers.Count} trackers");
                continue;
            }

            // Skip the balls that are outside the field
            if (IsOutside(raw.Detection.Position)) continue;

            var tracker = lastFilteredBall.HasValue
                ? new Tracker.Ball(raw, lastFilteredBall.Value)
                : new Tracker.Ball(raw)
                {
                    MaxDistance = 500
                };

            _ballTrackers.Add(tracker);
        }
    }
}
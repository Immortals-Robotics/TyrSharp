using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Time;

namespace Tyr.Vision;

[Configurable]
public partial class Camera(uint id)
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
    public DeltaTime DeltaTime => _frameTimeEstimator.DeltaTime ?? DeltaTime.Zero;
    public float Fps => (float)(1 / DeltaTime.Seconds);

    public Dictionary<RobotId, Tracker.Robot> Robots { get; } = [];
    public List<Tracker.Ball> Balls { get; } = [];

    private readonly DeltaTimeEstimator _frameTimeEstimator = new();

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
            Log.ZLogWarning($"Camera {Id} frame id mismatch, expected {expectedFrameId}, got {frame.FrameNumber}");

            if (Math.Abs((int)expectedFrameId - (int)frame.FrameNumber) > 10)
                Reset();
        }

        FrameId = frame.FrameNumber;

        // time
        Timestamp = frame.CaptureTime;

        _frameTimeEstimator.AddSample(frame.FrameNumber, frame.CaptureTime);

        Log.ZLogTrace($"Camera {Id} FPS: {Fps:F2}");

        Plot.Plot($"cam[{Id}] fps", Fps, "fps");

        // detections
        ProcessRobots(frame, lastFilteredFrame.Robots);
        ProcessBalls(frame, lastFilteredFrame.Ball);
    }

    private void Reset()
    {
        _frameTimeEstimator.Reset();
        Robots.Clear();
        Balls.Clear();
    }

    private bool IsOutside(Vector2 position)
    {
        return _fieldSize.HasValue && !_fieldSize.Value.FieldRectangleWithBoundary.Inside(position);
    }

    private void ProcessRobots(DetectionFrame frame, IReadOnlyList<FilteredRobot> filteredRobots)
    {
        // Remove trackers that are either too old or outside the field
        // can't directly remove from the dictionary while iterating over it, so we do it in two steps
        Robots.RemoveAll((_, tracker) =>
        {
            var tooOld = frame.CaptureTime - tracker.LastUpdateTimestamp >
                         DeltaTime.FromSeconds(InvisibleLifetimeRobot);

            var outsideField = IsOutside(tracker.GetPosition(frame.CaptureTime));

            return tooOld || outsideField;
        });

        // do a prediction on all trackers
        foreach (var r in Robots.Values)
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
            Log.ZLogDebug($"Ignoring robot {id} on camera {Id}");
        }
        else
        {
            if (Robots.TryGetValue(id, out var tracker))
            {
                // we already have a tracker for that robot, update it
                tracker.Update(robot);
            }
            else
            {
                // completely new robot on the field
                FilteredRobot? filteredRobot = null;
                foreach (var filtered in filteredRobots)
                {
                    if (filtered.Id != id) continue;

                    if (IsOutside(filtered.Position)) continue;

                    var close = Vector2.Distance(filtered.Position, robot.Detection.Position) < CopyTrackerMaxDistance;
                    if (!close) continue;

                    filteredRobot = filtered;
                    break;
                }

                tracker = filteredRobot != null
                    ? new Tracker.Robot(robot, filteredRobot.Value, color) // on a different camera already 
                    : new Tracker.Robot(robot, color); // completely new robot on the field


                Robots.Add(id, tracker);
            }
        }
    }

    private void ProcessBalls(DetectionFrame frame, FilteredBall? lastFilteredBall)
    {
        // remove trackers of balls that have not been visible or were out of the field for too long
        Balls.RemoveAll(tracker =>
            frame.CaptureTime - tracker.LastUpdateTimestamp > DeltaTime.FromSeconds(InvisibleLifetimeBall) ||
            frame.CaptureTime - tracker.LastInFieldTimestamp > DeltaTime.FromSeconds(InvisibleLifetimeBall));

        // do a prediction on all trackers
        foreach (var ball in Balls) ball.Predict(frame.CaptureTime);

        if (frame.Balls.Count > 0) LastBallOnCamTimestamp = Timestamp;

        // iterate over all balls on the camera
        foreach (var detection in frame.Balls)
        {
            var raw = new RawBall(detection, frame);

            var consumed = Balls.Any(t => t.Update(raw, _fieldSize?.FieldRectangle));
            if (consumed) continue;

            // This is a new ball, we need to create a new tracker for it
            Log.ZLogTrace($"Creating a new tracker for {raw} at {raw.Detection.Position}");

            // Skip if we already have too many trackers
            if (Balls.Count > MaxBallTrackers)
            {
                Log.ZLogWarning($"Skipping {raw} as we already have {Balls.Count} trackers");
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

            Balls.Add(tracker);
        }
    }
}
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Common.Math;
using Tyr.Common.Time;

namespace Tyr.Vision;

public class Camera(uint id)
{
    public uint Id { get; } = id;

    public uint FrameId { get; private set; }
    public DateTime Time { get; private set; }

    public float DeltaTime => (float)(_frameTimeEstimator.Estimate?.slope ?? 0f);

    private Dictionary<RobotId, Tracker.Robot> _robots = [];
    private List<Tracker.Ball> _balls = [];

    private const int FrameEstimatorHistoryCount = 100;
    private const int FrameEstimatorStride = 10;
    private readonly LineEstimator _frameTimeEstimator = new(FrameEstimatorHistoryCount);

    public void OnFrame(Frame frame)
    {
        // TODO: check frame id and update

        if (!_frameTimeEstimator.IsFull || frame.FrameNumber % FrameEstimatorStride == 0)
        {
            _frameTimeEstimator.AddSample(frame.FrameNumber, frame.CaptureTime.ToUnixTimeSeconds());
        }

        Logger.ZLogDebug($"Delta time: {DeltaTime}");

        ProcessRobots(frame);
        ProcessBalls(frame);
    }

    private void ProcessRobots(Frame frame)
    {
        // TODO: process robots
    }

    private void ProcessBalls(Frame frame)
    {
        // TODO: process balls
    }
}
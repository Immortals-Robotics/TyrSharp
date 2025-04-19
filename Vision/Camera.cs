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

    public float DeltaTime => (float)(_frameTimeEstimator.Estimate?.Slope ?? 0f);

    private Dictionary<RobotId, Tracker.Robot> _robots = [];
    private List<Tracker.Ball> _balls = [];

    private const int FrameEstimatorHistoryCount = 100;
    private const int FrameEstimatorStride = 10;
    private readonly LineEstimator _frameTimeEstimator = new(FrameEstimatorHistoryCount);

    public void OnFrame(Frame frame)
    {
        var expectedFrameId = FrameId == 0 ? frame.FrameNumber : FrameId + 1;
        if (frame.FrameNumber != expectedFrameId)
        {
            Logger.ZLogWarning($"Camera {Id} frame id mismatch, expected {expectedFrameId}, got {frame.FrameNumber}");

            if (Math.Abs((int)expectedFrameId - (int)frame.FrameNumber) > 10)
                Reset();
        }

        FrameId = frame.FrameNumber;

        if (!_frameTimeEstimator.IsFull || frame.FrameNumber % FrameEstimatorStride == 0)
        {
            _frameTimeEstimator.AddSample(frame.FrameNumber, frame.CaptureTime.ToUnixTimeSeconds());
        }

        Logger.ZLogTrace($"Camera {Id} Delta time: {DeltaTime}");

        ProcessRobots(frame);
        ProcessBalls(frame);
    }

    private void Reset()
    {
        _frameTimeEstimator.Reset();
        _robots.Clear();
        _balls.Clear();
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
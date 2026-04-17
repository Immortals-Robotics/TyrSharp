using Tyr.Common.Data.Ssl.Vision.Detection;

namespace Tyr.Vision;

internal static class DetectionFrameFilter
{
    public static Frame ApplyIgnoredHalf(Frame frame, IgnoreVisionHalf ignoredHalf)
    {
        if (ignoredHalf == IgnoreVisionHalf.None)
            return frame;

        var filteredBalls = frame.Balls.Where(ball => !ShouldIgnore(ball.X, ignoredHalf)).ToList();
        var filteredRobotsYellow = frame.RobotsYellow.Where(robot => !ShouldIgnore(robot.X, ignoredHalf)).ToList();
        var filteredRobotsBlue = frame.RobotsBlue.Where(robot => !ShouldIgnore(robot.X, ignoredHalf)).ToList();

        if (filteredBalls.Count == frame.Balls.Count &&
            filteredRobotsYellow.Count == frame.RobotsYellow.Count &&
            filteredRobotsBlue.Count == frame.RobotsBlue.Count)
        {
            return frame;
        }

        return new Frame
        {
            FrameNumber = frame.FrameNumber,
            CaptureTimeSeconds = frame.CaptureTimeSeconds,
            SentTimeSeconds = frame.SentTimeSeconds,
            CameraCaptureTimeSeconds = frame.CameraCaptureTimeSeconds,
            CameraId = frame.CameraId,
            Balls = filteredBalls,
            RobotsYellow = filteredRobotsYellow,
            RobotsBlue = filteredRobotsBlue
        };
    }

    private static bool ShouldIgnore(float x, IgnoreVisionHalf ignoredHalf)
    {
        return ignoredHalf switch
        {
            IgnoreVisionHalf.Left => x < 0f,
            IgnoreVisionHalf.Right => x > 0f,
            _ => false
        };
    }
}

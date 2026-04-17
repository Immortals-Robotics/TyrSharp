using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Vision;

namespace Tyr.Tests.Vision;

public class DetectionFrameFilterTests
{
    [Fact]
    public void ApplyIgnoredHalf_ReturnsSameFrame_WhenIgnoringIsDisabled()
    {
        var frame = CreateFrame();

        var result = DetectionFrameFilter.ApplyIgnoredHalf(frame, IgnoreVisionHalf.None);

        Assert.Same(frame, result);
    }

    [Fact]
    public void ApplyIgnoredHalf_RemovesLeftHalfDetections()
    {
        var frame = CreateFrame();

        var result = DetectionFrameFilter.ApplyIgnoredHalf(frame, IgnoreVisionHalf.Left);

        Assert.NotSame(frame, result);
        Assert.Collection(result.Balls, ball => Assert.Equal(0f, ball.X), ball => Assert.Equal(100f, ball.X));
        Assert.Collection(result.RobotsYellow, robot => Assert.Equal(0f, robot.X), robot => Assert.Equal(100f, robot.X));
        Assert.Collection(result.RobotsBlue, robot => Assert.Equal(0f, robot.X), robot => Assert.Equal(100f, robot.X));
    }

    [Fact]
    public void ApplyIgnoredHalf_RemovesRightHalfDetections()
    {
        var frame = CreateFrame();

        var result = DetectionFrameFilter.ApplyIgnoredHalf(frame, IgnoreVisionHalf.Right);

        Assert.NotSame(frame, result);
        Assert.Collection(result.Balls, ball => Assert.Equal(-100f, ball.X), ball => Assert.Equal(0f, ball.X));
        Assert.Collection(result.RobotsYellow, robot => Assert.Equal(-100f, robot.X), robot => Assert.Equal(0f, robot.X));
        Assert.Collection(result.RobotsBlue, robot => Assert.Equal(-100f, robot.X), robot => Assert.Equal(0f, robot.X));
    }

    private static Frame CreateFrame()
    {
        return new Frame
        {
            FrameNumber = 1,
            CaptureTimeSeconds = 1.0,
            SentTimeSeconds = 1.0,
            CameraId = 3,
            Balls =
            [
                CreateBall(-100f),
                CreateBall(0f),
                CreateBall(100f)
            ],
            RobotsYellow =
            [
                CreateRobot(1, -100f),
                CreateRobot(2, 0f),
                CreateRobot(3, 100f)
            ],
            RobotsBlue =
            [
                CreateRobot(4, -100f),
                CreateRobot(5, 0f),
                CreateRobot(6, 100f)
            ]
        };
    }

    private static Ball CreateBall(float x)
    {
        return new Ball
        {
            Confidence = 1f,
            X = x,
            Y = 10f,
            PixelX = 0f,
            PixelY = 0f
        };
    }

    private static Robot CreateRobot(uint id, float x)
    {
        return new Robot
        {
            Confidence = 1f,
            RobotId = id,
            X = x,
            Y = 10f,
            PixelX = 0f,
            PixelY = 0f
        };
    }
}

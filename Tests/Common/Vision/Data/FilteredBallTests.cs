using System.Numerics;
using Tyr.Common;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Trajectory;

namespace Tyr.Tests.Common.Vision.Data;

public class FilteredBallTests
{
    public FilteredBallTests()
    {
        ServiceLocator.BallTrajectoryFactory = new BallTrajectoryFactory();
    }

    [Fact]
    public void GetNextTouchdown_ReturnsTouchdown_ForChippedBall()
    {
        var ball = new FilteredBall
        {
            Timestamp = Timestamp.FromSeconds(1.0),
            LastVisibleTimestamp = Timestamp.FromSeconds(1.0),
            State = new BallState
            {
                Position3D = new Vector3(100f, -30f, 200f),
                Velocity = new Vector3(1500f, 300f, 800f),
                Acceleration = Vector3.Zero,
                SpinRadians = Vector2.Zero
            }
        };

        var touchdown = ball.GetNextTouchdown();

        Assert.NotNull(touchdown);
        Assert.True(touchdown!.Value.TimeUntilTouchdown.Seconds > 0);
        Assert.Equal(ball.Timestamp + touchdown.Value.TimeUntilTouchdown, touchdown.Value.GetTimestamp(ball.Timestamp));
    }

    [Fact]
    public void GetNextTouchdown_ReturnsNull_ForFlatBall()
    {
        var ball = new FilteredBall
        {
            Timestamp = Timestamp.FromSeconds(1.0),
            LastVisibleTimestamp = Timestamp.FromSeconds(1.0),
            State = new BallState
            {
                Position3D = new Vector3(100f, -30f, 0f),
                Velocity = new Vector3(1500f, 300f, 0f),
                Acceleration = Vector3.Zero,
                SpinRadians = Vector2.Zero
            }
        };

        Assert.Null(ball.GetNextTouchdown());
    }
}

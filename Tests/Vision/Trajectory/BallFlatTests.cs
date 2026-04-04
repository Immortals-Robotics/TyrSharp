using System.Numerics;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Trajectory;

namespace Tyr.Tests.Vision.Trajectory;

public class BallFlatTests
{
    private const float BallRadius = 21f;

    [Fact]
    public void GetState_StopsAtRestTime_ForFarFuturePredictions()
    {
        var trajectory = new BallFlat(new BallState
        {
            Position3D = new Vector3(100f, -50f, 0f),
            Velocity = new Vector3(1200f, 300f, 0f),
            Acceleration = Vector3.Zero,
            SpinRadians = Vector2.Zero
        });

        var stateAfterRest = trajectory.GetState(DeltaTime.FromSeconds(10));
        var stateFarAfterRest = trajectory.GetState(DeltaTime.FromSeconds(20));

        Assert.True(stateAfterRest.Velocity.Length() < 0.001f);
        Assert.True(stateFarAfterRest.Velocity.Length() < 0.001f);
        Assert.True(Vector3.Distance(stateAfterRest.Position3D, stateFarAfterRest.Position3D) < 0.001f);
        Assert.True(stateAfterRest.Acceleration.Length() > 0f);
        Assert.True(stateFarAfterRest.Acceleration.Length() > 0f);
    }

    [Fact]
    public void GetState_UsesSlidingModelAtKickTimestamp()
    {
        var trajectory = new BallFlat(new BallState
        {
            Position3D = new Vector3(100f, -50f, 0f),
            Velocity = new Vector3(1200f, 300f, 0f),
            Acceleration = Vector3.Zero,
            SpinRadians = Vector2.Zero
        });

        var stateAtKick = trajectory.GetState(DeltaTime.Zero);

        Assert.True(stateAtKick.Acceleration.Length() > 0f);
        Assert.True(stateAtKick.Acceleration.X < 0f);
        Assert.True(stateAtKick.Acceleration.Y < 0f);
        Assert.Equal(Vector3.Zero, trajectory.GetState(DeltaTime.FromSeconds(-0.1)).Acceleration);
    }
}

using System.Numerics;
using Tyr.Common.Time;
using Tyr.Vision.Trajectory;

namespace Tyr.Tests.Vision.Trajectory;

public class BallChipTests
{
    private const float Gravity = 9810f;
    private const float FirstHopDamping = 0.8f;
    private const float OtherHopDamping = 0.85f;
    private const float ZDamping = 0.47f;

    [Fact]
    public void GetState_FollowsBallisticArcDuringFirstHop()
    {
        var kickPosition = new Vector2(100f, -40f);
        var kickVelocity = new Vector3(2000f, 500f, 1500f);
        var trajectory = BallChip.FromKick(kickPosition, kickVelocity, Vector2.Zero);
        var queryTime = 0.1f;

        var state = trajectory.GetState(DeltaTime.FromSeconds(queryTime));

        Assert.InRange(state.Position.X, (kickPosition.X + (kickVelocity.X * queryTime)) - 0.001f, (kickPosition.X + (kickVelocity.X * queryTime)) + 0.001f);
        Assert.InRange(state.Position.Y, (kickPosition.Y + (kickVelocity.Y * queryTime)) - 0.001f, (kickPosition.Y + (kickVelocity.Y * queryTime)) + 0.001f);
        Assert.InRange(state.Position3D.Z, ((kickVelocity.Z * queryTime) - (0.5f * Gravity * queryTime * queryTime)) - 0.001f, ((kickVelocity.Z * queryTime) - (0.5f * Gravity * queryTime * queryTime)) + 0.001f);
        Assert.InRange(state.Velocity.Z, (kickVelocity.Z - (Gravity * queryTime)) - 0.001f, (kickVelocity.Z - (Gravity * queryTime)) + 0.001f);
        Assert.Equal(-Gravity, state.Acceleration.Z, 3);
    }

    [Fact]
    public void GetState_AppliesDifferentDampingOnFirstAndSecondHop()
    {
        var kickVelocity = new Vector3(2000f, 0f, 2500f);
        var trajectory = BallChip.FromKick(Vector2.Zero, kickVelocity, Vector2.Zero);
        var firstFlightTime = (2f * kickVelocity.Z) / Gravity;
        var secondFlightTime = (2f * kickVelocity.Z * ZDamping) / Gravity;

        var afterFirstHop = trajectory.GetState(DeltaTime.FromSeconds(firstFlightTime));
        var afterSecondHop = trajectory.GetState(DeltaTime.FromSeconds(firstFlightTime + secondFlightTime));

        Assert.InRange(afterFirstHop.Velocity.X, (kickVelocity.X * FirstHopDamping) - 0.01f, (kickVelocity.X * FirstHopDamping) + 0.01f);
        Assert.InRange(afterFirstHop.Velocity.Z, (kickVelocity.Z * ZDamping) - 0.01f, (kickVelocity.Z * ZDamping) + 0.01f);
        Assert.InRange(afterSecondHop.Velocity.X, (kickVelocity.X * FirstHopDamping * OtherHopDamping) - 0.01f, (kickVelocity.X * FirstHopDamping * OtherHopDamping) + 0.01f);
        Assert.InRange(afterSecondHop.Velocity.Z, (kickVelocity.Z * ZDamping * ZDamping) - 0.01f, (kickVelocity.Z * ZDamping * ZDamping) + 0.01f);
    }

    [Fact]
    public void GetState_StopsAtRest_ForFarFuturePredictions()
    {
        var trajectory = BallChip.FromKick(new Vector2(100f, -50f), new Vector3(1800f, 300f, 1600f), Vector2.Zero);

        var stateAfterRest = trajectory.GetState(DeltaTime.FromSeconds(10));
        var stateFarAfterRest = trajectory.GetState(DeltaTime.FromSeconds(20));

        Assert.True(stateAfterRest.Velocity.Length() < 0.001f);
        Assert.True(stateFarAfterRest.Velocity.Length() < 0.001f);
        Assert.True(Vector3.Distance(stateAfterRest.Position3D, stateFarAfterRest.Position3D) < 0.001f);
    }
}

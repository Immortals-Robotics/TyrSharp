using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Common.Time;
using Tyr.Common.Vision;
using Tyr.Common.Vision.Data;

namespace Tyr.Vision.Trajectory;

public class BallTrajectoryFactory : IBallTrajectoryFactory
{
    public IBallTrajectory Flat(BallState initial)
    {
        return new BallFlat(initial);
    }

    public IBallTrajectory FromState(BallState initial)
    {
        return initial.IsChipped
            ? new BallChip(initial)
            : new BallFlat(initial);
    }

    public BallState GetStateAt(BallState initial, DeltaTime dt)
    {
        if (initial.IsChipped)
        {
            var chip = new BallChip(initial);
            return chip.GetState(dt);
        }

        var flat = new BallFlat(initial);
        return flat.GetState(dt);
    }

    public IBallTrajectory FromKickedBall(Vector2 position, Vector3 velocity, Vector2 spin)
    {
        if (velocity.Z > 0f)
        {
            return BallChip.FromKick(position, velocity, spin);
        }

        return new BallFlat(new BallState
        {
            Position3D = position.Xyz(),
            Velocity = velocity,
            Acceleration = Vector3.Zero,
            SpinRadians = spin
        });
    }

    public IBallTrajectory FromKickedBallWithoutSpin(Vector2 position, Vector3 velocity)
    {
        return FromKickedBall(position, velocity, Vector2.Zero);
    }
}

using System.Numerics;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;

namespace Tyr.Common.Vision;

public interface IBallTrajectoryFactory
{
    public IBallTrajectory Flat(BallState initial);

    public IBallTrajectory FromState(BallState initial);

    public BallState GetStateAt(BallState initial, DeltaTime dt);

    public IBallTrajectory FromKickedBall(Vector2 position, Vector3 velocity, Vector2 spin);

    public IBallTrajectory FromKickedBallWithoutSpin(Vector2 position, Vector3 velocity);
}

using System.Numerics;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Navigation.Trajectory;

public static class Trajectory2DFullStop
{
    public static Trajectory2D Make(Vector2 p0, Vector2 v0, VelocityProfile profile)
    {
        if (v0.LengthSquared() > 1e-6f)
        {
            var duration = v0.Length() / profile.Acceleration;
            var accVec = Vector2.Normalize(v0) * -profile.Acceleration;

            var trajectoryX = Trajectory1DPieced.Create(new Trajectory1DConstantAcc
            {
                StartTime = 0,
                EndTime = duration,
                Acceleration = accVec.X,
                StartVelocity = v0.X,
                StartPosition = p0.X
            });

            var trajectoryY = Trajectory1DPieced.Create(new Trajectory1DConstantAcc
            {
                StartTime = 0,
                EndTime = duration,
                Acceleration = accVec.Y,
                StartVelocity = v0.Y,
                StartPosition = p0.Y
            });

            return new Trajectory2D
            {
                TrajectoryX = trajectoryX,
                TrajectoryY = trajectoryY,
            };
        }

        var trajectory = new Trajectory2D()
        {
            TrajectoryX = Trajectory1DPieced.Empty,
            TrajectoryY = Trajectory1DPieced.Empty,
        };
        return trajectory;
    }
}

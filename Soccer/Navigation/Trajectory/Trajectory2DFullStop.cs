using System.Numerics;

namespace Tyr.Soccer.Navigation.Trajectory;

public static class Trajectory2DFullStop
{
    public static Trajectory2D Make(Vector2 p0, Vector2 v0, VelocityProfile profile)
    {
        var trajectoryX = new Trajectory1DPieced();
        var trajectoryY = new Trajectory1DPieced();

        if (v0.LengthSquared() > 1e-6f)
        {
            var duration = v0.Length() / profile.Acceleration;
            var accVec = Vector2.Normalize(v0) * -profile.Acceleration;

            trajectoryX.AddPiece(new Trajectory1DConstantAcc
            {
                StartTime = 0,
                EndTime = duration,
                Acceleration = accVec.X,
                StartVelocity = v0.X,
                StartPosition = p0.X
            });

            trajectoryY.AddPiece(new Trajectory1DConstantAcc
            {
                StartTime = 0,
                EndTime = duration,
                Acceleration = accVec.Y,
                StartVelocity = v0.Y,
                StartPosition = p0.Y
            });
        }

        var trajectory = new Trajectory2D()
        {
            TrajectoryX = trajectoryX,
            TrajectoryY = trajectoryY,
        };
        return trajectory;
    }
}
using System.Numerics;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Navigation.Trajectory;

public static class TrajectoryBangBang
{
    private static float VelChangeToZero(float s0, float v0, float aMax)
    {
        var a = v0 <= 0 ? aMax : -aMax;
        var t = -v0 / a;
        return s0 + 0.5f * v0 * t;
    }

    private static float VelTriToZero(float s0, float v0, float v1, float aMax)
    {
        var (a1, a2) = v1 >= v0 ? (aMax, -aMax) : (-aMax, aMax);
        var t1 = (v1 - v0) / a1;
        var s1 = s0 + 0.5f * (v0 + v1) * t1;
        var t2 = -v1 / a2;
        return s1 + 0.5f * v1 * t2;
    }

    private static Trajectory1DPieced CalcTri(float s0, float v0, float s2, float a)
    {
        var sq = a > 0
            ? (a * (s2 - s0) + 0.5f * v0 * v0) / (a * a)
            : (-a * (s0 - s2) + 0.5f * v0 * v0) / (a * a);

        var t2 = sq > 0 ? MathF.Sqrt(sq) : 0f;
        var v1 = a * t2;
        var t1 = (v1 - v0) / a;
        var s1 = s0 + 0.5f * (v0 + v1) * t1;

        var p0 = new Trajectory1DConstantAcc
        {
            StartTime = 0,
            EndTime = t1,
            Acceleration = a,
            StartVelocity = v0,
            StartPosition = s0
        };

        var p1 = new Trajectory1DConstantAcc
        {
            StartTime = t1,
            EndTime = t1 + t2,
            Acceleration = -a,
            StartVelocity = v1,
            StartPosition = s1
        };

        return Trajectory1DPieced.Create(p0, p1);
    }

    private static Trajectory1DPieced CalcTrapz(float s0, float v0, float v1, float s3, float aMax)
    {
        var a1 = v0 > v1 ? -aMax : aMax;
        var a3 = v1 > 0 ? -aMax : aMax;

        var t1 = (v1 - v0) / a1;
        var v2 = v1;
        var t3 = -v2 / a3;

        var s1 = s0 + 0.5f * (v0 + v1) * t1;
        var s2 = s3 - 0.5f * v2 * t3;
        var t2 = (s2 - s1) / v1;

        var p0 = new Trajectory1DConstantAcc
        {
            StartTime = 0,
            EndTime = t1,
            Acceleration = a1,
            StartVelocity = v0,
            StartPosition = s0
        };

        var p1 = new Trajectory1DConstantAcc
        {
            StartTime = t1,
            EndTime = t1 + t2,
            Acceleration = 0,
            StartVelocity = v1,
            StartPosition = s1
        };

        var p2 = new Trajectory1DConstantAcc
        {
            StartTime = t1 + t2,
            EndTime = t1 + t2 + t3,
            Acceleration = a3,
            StartVelocity = v2,
            StartPosition = s2
        };

        return Trajectory1DPieced.Create(p0, p1, p2);
    }

    public static Trajectory1DPieced Make1D(
        float x0, float xTrg, float xd0,
        float xdMax, float xddMax)
    {
        var sAtZeroAcc = VelChangeToZero(x0, xd0, xddMax);

        if (sAtZeroAcc <= xTrg)
        {
            var sEnd = VelTriToZero(x0, xd0, xdMax, xddMax);
            return sEnd >= xTrg
                ? CalcTri(x0, xd0, xTrg, xddMax)
                : CalcTrapz(x0, xd0, xdMax, xTrg, xddMax);
        }
        else
        {
            var sEnd = VelTriToZero(x0, xd0, -xdMax, xddMax);
            return sEnd <= xTrg
                ? CalcTri(x0, xd0, xTrg, -xddMax)
                : CalcTrapz(x0, xd0, -xdMax, xTrg, xddMax);
        }
    }

    public static Trajectory2D Make2D(Vector2 s0, Vector2 v0, Vector2 s1, VelocityProfile profile)
    {
        var accuracy = 1e-3f;
        var vmax = profile.Speed;
        var acc = profile.Acceleration;

        var inc = MathF.PI / 8f;
        var alpha = MathF.PI / 4f;

        Trajectory1DPieced trajectoryX = new();
        Trajectory1DPieced trajectoryY = new();

        while (inc > 1e-7f)
        {
            var (sA, cA) = MathF.SinCos(alpha);

            trajectoryX = Make1D(s0.X, s1.X, v0.X, vmax * cA, acc * cA);
            trajectoryY = Make1D(s0.Y, s1.Y, v0.Y, vmax * sA, acc * sA);

            var dx = trajectoryX.Duration;
            var dy = trajectoryY.Duration;

            var diff = MathF.Abs(dx - dy);
            if (diff < accuracy) break;

            alpha += dx > dy ? -inc : inc;
            inc *= 0.5f;
        }

        Trajectory2D trajectory = new()
        {
            TrajectoryX = trajectoryX,
            TrajectoryY = trajectoryY,
        };

        return trajectory;
    }
}

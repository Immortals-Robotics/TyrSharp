using System.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Time;
using Tyr.Vision.Data;
using Tyr.Vision.Util;

namespace Tyr.Vision.Estimators.Chip;

public class ChipKickLinearTimeOffsetFitter
{
    private const double Gravity = 9810.0;

    private readonly Vector2 _kickPosition;
    private Timestamp _kickTimestamp;
    private readonly IReadOnlyDictionary<uint, CameraCalibration> _cameraCalibrations;

    public ChipKickLinearTimeOffsetFitter(
        Vector2 kickPosition,
        Timestamp kickTimestamp,
        IReadOnlyDictionary<uint, CameraCalibration> cameraCalibrations)
    {
        _kickPosition = kickPosition;
        _kickTimestamp = kickTimestamp;
        _cameraCalibrations = cameraCalibrations;
    }

    public double LastL1Error { get; private set; }

    public SolvedKick? Solve(List<RawBall> ballRecords)
    {
        if (ballRecords.Count < 2)
        {
            return null;
        }

        var timeOffset = 0.05;
        var increment = timeOffset / 2.0;

        while (increment > 1e-3)
        {
            var negative = LinearSolve(ballRecords, timeOffset - 1e-5);
            var positive = LinearSolve(ballRecords, timeOffset + 1e-5);

            if ((negative == null) || (positive == null))
            {
                return null;
            }

            if (negative.L1Error > positive.L1Error)
            {
                timeOffset += increment;
            }
            else
            {
                timeOffset -= increment;
            }

            increment /= 2.0;
        }

        return PostProcess(ballRecords, LinearSolve(ballRecords, timeOffset));
    }

    private LinearSolveResult? LinearSolve(List<RawBall> ballRecords, double timeOffset)
    {
        var numRecords = ballRecords.Count;
        var matrix = new DenseMatrix(numRecords * 2, 3);
        var rhs = new DenseVector(numRecords * 2);
        var tZero = ballRecords[0].CaptureTimestamp;

        for (var i = 0; i < numRecords; i++)
        {
            var ballRecord = ballRecords[i];
            var time = (ballRecord.CaptureTimestamp - tZero).Seconds + timeOffset;
            var cameraPosition = BallProjection.GetCameraPosition(_cameraCalibrations, ballRecord.CameraId);
            var measurement = ballRecord.Detection.Position;

            matrix.SetRow(i * 2, [cameraPosition.Z * time, 0.0, (measurement.X - cameraPosition.X) * time]);
            matrix.SetRow((i * 2) + 1, [0.0, cameraPosition.Z * time, (measurement.Y - cameraPosition.Y) * time]);

            rhs[i * 2] = ((0.5 * Gravity * time * time * (measurement.X - cameraPosition.X))
                          + (measurement.X * cameraPosition.Z))
                         - (_kickPosition.X * cameraPosition.Z);
            rhs[(i * 2) + 1] = ((0.5 * Gravity * time * time * (measurement.Y - cameraPosition.Y))
                                + (measurement.Y * cameraPosition.Z))
                               - (_kickPosition.Y * cameraPosition.Z);
        }

        try
        {
            var solution = matrix.QR().Solve(rhs);
            var residual = matrix.Multiply(solution).Subtract(rhs);
            return new LinearSolveResult(solution, residual.L1Norm(), timeOffset);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private SolvedKick? PostProcess(List<RawBall> ballRecords, LinearSolveResult? bestResult)
    {
        if (bestResult == null)
        {
            return null;
        }

        var kickVelocity = new Vector3(
            (float)bestResult.Solution[0],
            (float)bestResult.Solution[1],
            (float)bestResult.Solution[2]);

        if (kickVelocity.Xy().Length() < 100f)
        {
            return null;
        }

        if (kickVelocity.Z < 0f)
        {
            return null;
        }

        _kickTimestamp = ballRecords[0].CaptureTimestamp - DeltaTime.FromSeconds(bestResult.TimeOffset);
        LastL1Error = ComputeL1Error(ballRecords, kickVelocity, _kickTimestamp);

        return new SolvedKick(
            _kickPosition,
            kickVelocity,
            _kickTimestamp,
            Vector2.Zero,
            nameof(ChipKickLinearTimeOffsetFitter));
    }

    private double ComputeL1Error(List<RawBall> ballRecords, Vector3 kickVelocity, Timestamp kickTimestamp)
    {
        var error = 0.0;
        var kickPosition3D = _kickPosition.Xyz();
        var acceleration = new Vector3(0f, 0f, (float)(-0.5 * Gravity));

        foreach (var ballRecord in ballRecords)
        {
            var time = (float)(ballRecord.CaptureTimestamp - kickTimestamp).Seconds;
            var modelPosition = kickPosition3D + (kickVelocity * time) + (acceleration * (time * time));
            var ground = BallProjection.ProjectToGround(
                modelPosition,
                BallProjection.GetCameraPosition(_cameraCalibrations, ballRecord.CameraId));
            var delta = ground - ballRecord.Detection.Position;
            error += Math.Abs(delta.X) + Math.Abs(delta.Y);
        }

        return error;
    }

    private sealed record LinearSolveResult(
        MathNet.Numerics.LinearAlgebra.Vector<double> Solution,
        double L1Error,
        double TimeOffset);
}

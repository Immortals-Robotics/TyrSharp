using System.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Optimization;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Time;
using Tyr.Vision.Data;
using Tyr.Vision.Util;

namespace Tyr.Vision.Calibrators;

public class StraightBallModelCalibrator
{
    private const double MinKickSpeed = 100.0;
    private const double MaxKickSpeed = 10000.0;
    private const double MinAccelerationSlide = -8000.0;
    private const double MaxAccelerationSlide = -100.0;
    private const double MinAccelerationRoll = -2000.0;
    private const double MaxAccelerationRoll = -10.0;
    private const double MinSwitchFactor = 0.5;
    private const double MaxSwitchFactor = 0.85;

    private const double InitialKickSpeedStep = 10.0;
    private const double AccelerationSlideStep = 10.0;
    private const double AccelerationRollStep = 1.0;
    private const double SwitchFactorStep = 0.01;
    private const double FunctionTolerance = 1e-6;
    private const int MaxIterations = 2000;

    public StraightBallModelCalibration? Calibrate(List<RawBall> ballRecords)
    {
        if (ballRecords.Count < 2)
            return null;

        var estimated = BallHelpers.GetKickDirection(ballRecords
            .Select(record => record.Detection.Position)
            .ToList());
        if (estimated == null)
            return null;

        var velocities = CalculateVelocities(ballRecords);
        if (velocities.Count == 0)
            return null;

        var (_, kickDirection) = estimated.Value;
        var initialGuess = DenseVector.OfArray(
        [
            velocities[0].Velocity,
            BallParameters.AccelerationSlide,
            BallParameters.AccelerationRoll,
            BallParameters.KSwitch
        ]);
        var lowerBounds = DenseVector.OfArray(
        [
            MinKickSpeed,
            MinAccelerationSlide,
            MinAccelerationRoll,
            MinSwitchFactor
        ]);
        var upperBounds = DenseVector.OfArray(
        [
            MaxKickSpeed,
            MaxAccelerationSlide,
            MaxAccelerationRoll,
            MaxSwitchFactor
        ]);

        var model = new StraightBallModel(velocities);

        double[] parameters;
        try
        {
            var optimizer = new NelderMeadSimplex(FunctionTolerance, MaxIterations);
            var objective = ObjectiveFunction.Value(
                point => model.Value(point, lowerBounds, upperBounds));
            var optimum = optimizer.FindMinimum(
                objective,
                initialGuess,
                DenseVector.OfArray(
                [
                    InitialKickSpeedStep,
                    AccelerationSlideStep,
                    AccelerationRollStep,
                    SwitchFactorStep
                ]));
            parameters = optimum.MinimizingPoint.ToArray();
        }
        catch (Exception)
        {
            return null;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i] <= lowerBounds[i] || parameters[i] >= upperBounds[i])
            {
                return null;
            }
        }

        return new StraightBallModelCalibration(
            new Vector3(
                kickDirection.X * (float)parameters[0],
                kickDirection.Y * (float)parameters[0],
                0f),
            ballRecords[0].Detection.Position,
            ballRecords[0].CaptureTimestamp,
            new BallModelStraightTwoPhase
            {
                AccSlide = parameters[1],
                AccRoll = parameters[2],
                KSwitch = parameters[3]
            },
            ballRecords.Count);
    }

    private static List<TimeVelocityPair> CalculateVelocities(List<RawBall> ballRecords)
    {
        var velocities = new List<TimeVelocityPair>();

        foreach (var group in ballRecords
                     .GroupBy(record => record.CameraId)
                     .Select(group => group.OrderBy(record => record.CaptureTimestamp).ToList()))
        {
            for (var i = 1; i < group.Count; i++)
            {
                var previous = group[i - 1];
                var next = group[i];

                var deltaTime = (next.CaptureTimestamp - previous.CaptureTimestamp).Seconds;
                if (deltaTime < 1e-3)
                    continue;

                var deltaPosition = Vector2.Distance(previous.Detection.Position, next.Detection.Position);
                var centralTimestamp = Timestamp.FromNanoseconds(
                    (previous.CaptureTimestamp.Nanoseconds + next.CaptureTimestamp.Nanoseconds) / 2);

                velocities.Add(new TimeVelocityPair(centralTimestamp, deltaPosition / deltaTime));
            }
        }

        velocities.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return velocities;
    }

    public sealed record StraightBallModelCalibration(
        Vector3 KickVelocity,
        Vector2 KickPosition,
        Timestamp KickTimestamp,
        BallModelStraightTwoPhase Model,
        int SampleAmount) : IKickModelIdentification
    {
        public double AccSlide => Model.AccSlide;
        public double AccRoll => Model.AccRoll;
        public double KSwitch => Model.KSwitch;

        public static string[] ParameterNames() => ["accSlide", "accRoll", "kSwitch"];
    }

    private readonly record struct TimeVelocityPair(Timestamp Timestamp, double Velocity);

    private sealed class StraightBallModel(List<TimeVelocityPair> velocities)
    {
        public double Value(
            MathNet.Numerics.LinearAlgebra.Vector<double> point,
            MathNet.Numerics.LinearAlgebra.Vector<double> lowerBounds,
            MathNet.Numerics.LinearAlgebra.Vector<double> upperBounds)
        {
            if (point.Count != 4)
            {
                return double.MaxValue;
            }

            var penalty = 0.0;
            for (var i = 0; i < point.Count; i++)
            {
                if (point[i] < lowerBounds[i])
                {
                    penalty += Math.Pow(lowerBounds[i] - point[i], 2);
                }
                else if (point[i] > upperBounds[i])
                {
                    penalty += Math.Pow(point[i] - upperBounds[i], 2);
                }
            }

            if (penalty > 0)
            {
                return 1e12 + penalty;
            }

            var kickVelocity = point[0];
            var accelerationSlide = point[1];
            var accelerationRoll = point[2];
            var switchFactor = point[3];

            var switchVelocity = kickVelocity * switchFactor;
            var switchTime = (kickVelocity * (switchFactor - 1.0)) / accelerationSlide;
            var tZero = velocities[0].Timestamp;

            double error = 0;

            foreach (var entry in velocities)
            {
                var t = (entry.Timestamp - tZero).Seconds;
                var modelVelocity = t < switchTime
                    ? kickVelocity + (t * accelerationSlide)
                    : switchVelocity + ((t - switchTime) * accelerationRoll);

                error += Math.Pow(entry.Velocity - modelVelocity, 2);
            }

            return Math.Sqrt(error) / velocities.Count;
        }
    }
}

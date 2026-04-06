using System.Diagnostics.CodeAnalysis;
using NumFlat;

namespace Tyr.Common.Math;

//
// A discrete‑time linear Kalman filter
// x[k]   = A·x[k‑1] + B·u[k‑1] + w[k‑1]
// z[k]   = H·x[k]   + v[k]
//
// w and v are process and measurement noise and are zero‑mean,
// mutually independent, white‑noise vectors whose covariances
// are Q and R, respectively.

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class KalmanFilter(int numStates, int numMeasurements, int numControl)
{
    // internal state
    public Vec<double> StateEstimate { get; private set; } = new(numStates); // x̂
    public Mat<double> ErrorCovariance { get; set; } = new(numStates, numStates); // P
    public Vec<double> Innovation { get; private set; } = new(numMeasurements);

    // model matrices
    public Mat<double> TransitionMatrix { get; set; } = new(numStates, numStates); // A
    public Mat<double> ControlMatrix { get; set; } = new(numStates, numControl); // B
    public Mat<double> ProcessNoiseCovariance { get; set; } = new(numStates, numStates); // Q
    public Mat<double> MeasurementMatrix { get; set; } = new(numMeasurements, numStates); // H

    public Mat<double> MeasurementNoiseCovariance { get; set; } =
        new(numMeasurements, numMeasurements); // R

    // Projects the state estimate one step ahead.
    public void Predict() => Predict(control: null);

    // Projects the state estimate one step ahead, with optional control input.
    public void Predict(Vec<double>? control)
    {
        // x̂(k|k‑1) = A·x̂(k‑1|k‑1) + B·u(k‑1)
        StateEstimate = TransitionMatrix * StateEstimate;
        if (control is not null)
            StateEstimate += ControlMatrix * control.Value;

        // P(k|k‑1) = A·P(k‑1|k‑1)·Aᵀ + Q
        ErrorCovariance = TransitionMatrix * ErrorCovariance * TransitionMatrix.Transpose() + ProcessNoiseCovariance;
    }

    // Incorporates a measurement into the current state estimate.
    public void Correct(Vec<double> measurement)
    {
        // S = H·P·Hᵀ + R
        var S = MeasurementMatrix * ErrorCovariance * MeasurementMatrix.Transpose() + MeasurementNoiseCovariance;

        // ν = z - H·x̂
        Innovation = measurement - MeasurementMatrix * StateEstimate;

        // K = P·Hᵀ·S⁻¹   computed without forming S⁻¹
        var K = S.Qr()
            .Solve(MeasurementMatrix * ErrorCovariance.Transpose())
            .Transpose();

        // x̂ = x̂ + K·ν
        StateEstimate += K * Innovation;

        // P = (I − K·H)·P
        var I = new Mat<double>(K.RowCount, K.RowCount);
        for (var i = 0; i < K.RowCount; i++)
        {
            I[i, i] = 1.0;
        }
        ErrorCovariance = (I - K * MeasurementMatrix) * ErrorCovariance;
    }
}
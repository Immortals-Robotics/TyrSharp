using MathNet.Numerics.LinearAlgebra;

namespace Tyr.Common.Math;

//
// A discrete‑time linear Kalman filter
// x[k]   = A·x[k‑1] + B·u[k‑1] + w[k‑1]
// z[k]   = H·x[k]   + v[k]
//
// w and v are process and measurement noise and are zero‑mean,
// mutually independent, white‑noise vectors whose covariances
// are Q and R, respectively.

public class KalmanFilter
{
    // internal state
    public Vector<double> StateEstimate { get; private set; } // x̂
    public Matrix<double> ErrorCovariance { get; private set; } // P
    public Vector<double> Innovation { get; private set; }

    // model matrices
    public Matrix<double> TransitionMatrix { get; set; } // A
    public Matrix<double> ControlMatrix { get; set; } // B
    public Matrix<double> ProcessNoiseCovariance { get; set; } // Q
    public Matrix<double> MeasurementMatrix { get; set; } // H
    public Matrix<double> MeasurementNoiseCovariance { get; set; } // R

    public KalmanFilter(int numStates, int numMeasurements, int numControl)
    {
        StateEstimate = Vector<double>.Build.Dense(numStates);
        Innovation = Vector<double>.Build.Dense(numMeasurements);

        TransitionMatrix = Matrix<double>.Build.Dense(numStates, numStates);
        ErrorCovariance = Matrix<double>.Build.Dense(numStates, numStates);
        ProcessNoiseCovariance = Matrix<double>.Build.Dense(numStates, numStates);

        ControlMatrix = Matrix<double>.Build.Dense(numStates, numControl);

        MeasurementMatrix = Matrix<double>.Build.Dense(numMeasurements, numStates);
        MeasurementNoiseCovariance = Matrix<double>.Build.Dense(numMeasurements, numMeasurements);
    }

    public KalmanFilter(KalmanFilter source)
    {
        StateEstimate = source.StateEstimate.Clone();
        Innovation = source.Innovation.Clone();

        TransitionMatrix = source.TransitionMatrix.Clone();
        ErrorCovariance = source.ErrorCovariance.Clone();
        ProcessNoiseCovariance = source.ProcessNoiseCovariance.Clone();

        ControlMatrix = source.ControlMatrix.Clone();

        MeasurementMatrix = source.MeasurementMatrix.Clone();
        MeasurementNoiseCovariance = source.MeasurementNoiseCovariance.Clone();
    }

    // Projects the state estimate one step ahead.
    public void Predict() => Predict(control: null);

    // Projects the state estimate one step ahead, with optional control input.
    public void Predict(Vector<double>? control)
    {
        // x̂(k|k‑1) = A·x̂(k‑1|k‑1) + B·u(k‑1)
        StateEstimate = TransitionMatrix * StateEstimate;
        if (control is not null)
            StateEstimate += ControlMatrix * control;

        // P(k|k‑1) = A·P(k‑1|k‑1)·Aᵀ + Q
        ErrorCovariance = TransitionMatrix * ErrorCovariance * TransitionMatrix.Transpose() + ProcessNoiseCovariance;
    }

    // Incorporates a measurement into the current state estimate.
    public void Correct(Vector<double> measurement)
    {
        // S = H·P·Hᵀ + R
        var S = MeasurementMatrix * ErrorCovariance * MeasurementMatrix.Transpose() + MeasurementNoiseCovariance;

        // ν = z - H·x̂
        Innovation = measurement - MeasurementMatrix * StateEstimate;

        // K = P·Hᵀ·S⁻¹   computed without forming S⁻¹
        var K = S.QR()
            .Solve(MeasurementMatrix * ErrorCovariance.Transpose())
            .Transpose();

        // x̂ = x̂ + K·ν
        StateEstimate += K * Innovation;

        // P = (I − K·H)·P
        var I = Matrix<double>.Build.DenseIdentity(K.RowCount);
        ErrorCovariance = (I - K * MeasurementMatrix) * ErrorCovariance;
    }
}
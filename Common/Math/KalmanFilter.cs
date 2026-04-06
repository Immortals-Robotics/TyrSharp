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
    private Vec<double> _stateEstimate = new(numStates);
    private Mat<double> _errorCovariance = new(numStates, numStates);

    // scratch buffers reused each tick to avoid per-call allocations
    private Vec<double> _tempState = new(numStates);
    private readonly Vec<double> _tempControl = new(numStates);
    private Mat<double> _tempMat1 = new(numStates, numStates);
    private readonly Mat<double> _tempMat2 = new(numStates, numStates);
    private readonly Vec<double> _innovation = new(numMeasurements);
    private readonly Mat<double> _S = new(numMeasurements, numMeasurements);
    private readonly Mat<double> _tempMeasMat = new(numMeasurements, numStates);
    private readonly Mat<double> _KT = new(numMeasurements, numStates);
    private readonly Mat<double> _K = new(numStates, numMeasurements);

    public Vec<double> StateEstimate => _stateEstimate;
    public Mat<double> ErrorCovariance { get => _errorCovariance; set => _errorCovariance = value; }
    public Vec<double> Innovation => _innovation;

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
        var tm = TransitionMatrix;

        // x̂(k|k‑1) = A·x̂(k‑1|k‑1) + B·u(k‑1)
        Mat.Mul(tm, _stateEstimate, _tempState, transposeX: false);
        if (control is not null)
        {
            var cm = ControlMatrix;
            var u = control.Value;
            Mat.Mul(cm, u, _tempControl, transposeX: false);
            _tempState.AddInplace(_tempControl);
        }
        (_stateEstimate, _tempState) = (_tempState, _stateEstimate);

        // P(k|k‑1) = A·P(k‑1|k‑1)·Aᵀ + Q
        Mat.Mul(tm, _errorCovariance, _tempMat1, transposeX: false, transposeY: false);
        Mat.Mul(_tempMat1, tm, _tempMat2, transposeX: false, transposeY: true);
        var q = ProcessNoiseCovariance;
        Mat.Add(_tempMat2, q, _tempMat1);
        (_errorCovariance, _tempMat1) = (_tempMat1, _errorCovariance);
    }

    // Incorporates a measurement into the current state estimate.
    public void Correct(Vec<double> measurement)
    {
        var hm = MeasurementMatrix;

        // S = H·P·Hᵀ + R
        Mat.Mul(hm, _errorCovariance, _tempMeasMat, transposeX: false, transposeY: false); // H·P (m×n)
        Mat.Mul(_tempMeasMat, hm, _S, transposeX: false, transposeY: true);                // H·P·Hᵀ (m×m)
        _S.AddInplace(MeasurementNoiseCovariance);                                         // += R

        // ν = z − H·x̂
        Mat.Mul(hm, _stateEstimate, _innovation, transposeX: false); // H·x̂
        _innovation.MulInplace(-1.0);                                 // −H·x̂
        _innovation.AddInplace(measurement);                          // z − H·x̂

        // K = P·Hᵀ·S⁻¹  via  Kᵀ = S⁻¹·H·Pᵀ
        Mat.Mul(hm, _errorCovariance, _tempMeasMat, transposeX: false, transposeY: true); // H·Pᵀ (m×n)
        _S.Qr().Solve(_tempMeasMat, _KT);                                                 // Kᵀ = S⁻¹·H·Pᵀ (m×n)
        Mat.Transpose(_KT, _K);                                                            // K (n×m)

        // x̂ = x̂ + K·ν
        Mat.Mul(_K, _innovation, _tempState, transposeX: false); // K·ν (n-vec)
        _stateEstimate.AddInplace(_tempState);

        // P = (I − K·H)·P  ≡  P −= K·H·P
        Mat.Mul(_K, hm, _tempMat1, transposeX: false, transposeY: false); // K·H (n×n)
        Mat.Mul(_tempMat1, _errorCovariance, _tempMat2, transposeX: false, transposeY: false); // K·H·P (n×n)
        _errorCovariance.SubInplace(_tempMat2);                                               // P −= K·H·P
    }
}
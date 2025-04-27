using Tyr.Common.Math;
using Tyr.Common.Time;

namespace Tyr.Vision.Filter;

//  A 1D position‑&‑velocity tracking filter:
//
//      state  x = [ p  v ]ᵀ
//      meas   z = [ p ]ᵀ
//
//  – constant‑velocity model
//  – white‑noise acceleration with tunable variance
public class Filter1D
{
    public Timestamp LastTimestamp { get; private set; }

    private readonly float _modelError;
    private readonly KalmanFilter _kalman;

    /// <summary>
    /// Initialize with pos-only measurement, zero velocity.
    /// </summary>
    public Filter1D(float initialPosition,
        float covariance, float modelError, float measurementError,
        Timestamp timestamp)
    {
        _kalman = new KalmanFilter(2, 1, 1);

        Position = initialPosition;

        PositionErrorCovariance = covariance;
        VelocityErrorCovariance = covariance;

        _kalman.MeasurementMatrix[0, 0] = 1.0;

        MeasurementError = measurementError;
        _modelError = modelError;

        LastTimestamp = timestamp;
    }

    /// <summary>
    /// Initialize with both position and velocity specified
    /// </summary>
    public Filter1D(float initialPosition, float initialVelocity,
        float covariance, float modelError, float measurementError,
        Timestamp timestamp)
        : this(initialPosition, covariance, modelError, measurementError, timestamp)
    {
        Velocity = initialVelocity;
    }

    /// <summary>
    /// Predict forward to a new timestamp.
    /// </summary>
    public void Predict(Timestamp timestamp)
    {
        var dt = timestamp - LastTimestamp;
        if (dt.Nanoseconds <= 0) return;

        LastTimestamp = timestamp;
        UpdateMatrices(dt);
        _kalman.Predict();
    }

    /// <summary>
    /// Feed in a new position measurement.
    /// </summary>
    public void Correct(float position)
    {
        var vector = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(1);
        vector[0] = position;
        _kalman.Correct(vector);
    }

    public float Position
    {
        get => (float)_kalman.StateEstimate[0];
        private set => _kalman.StateEstimate[0] = value;
    }

    public float Velocity
    {
        get => (float)_kalman.StateEstimate[1];
        private set => _kalman.StateEstimate[1] = value;
    }

    public float GetPosition(Timestamp timestamp)
    {
        var dt = timestamp - LastTimestamp;
        return Position + Velocity * (float)dt.Seconds;
    }
    
    private float MeasurementError
    {
        get => (float)_kalman.MeasurementNoiseCovariance[0, 0];
        set => _kalman.MeasurementNoiseCovariance[0, 0] = value;
    }

    public float PositionErrorCovariance
    {
        get => (float)_kalman.ErrorCovariance[0, 0];
        set => _kalman.ErrorCovariance[0, 0] = value;
    }

    public float VelocityErrorCovariance
    {
        get => (float)_kalman.ErrorCovariance[1, 1];
        set => _kalman.ErrorCovariance[1, 1] = value;
    }

    public float PositionUncertainty => MathF.Sqrt(PositionErrorCovariance);
    public float VelocityUncertainty => MathF.Sqrt(VelocityErrorCovariance);

    public float PositionInnovation => (float)_kalman.Innovation[0];

    /// <summary>
    /// Build F and Q for constant‑vel model with white accel noise.
    /// </summary>
    private void UpdateMatrices(DeltaTime dt)
    {
        var dtSeconds = dt.Seconds;

        _kalman.TransitionMatrix[0, 0] = 1;
        _kalman.TransitionMatrix[0, 1] = dtSeconds;
        _kalman.TransitionMatrix[1, 0] = 0;
        _kalman.TransitionMatrix[1, 1] = 1;

        // set optimal process noise σ² Q for white‑noise accel: see Simon, “Optimal State Estimation.”
        // here error = E[a²] * dt
        var sigma = Math.Sqrt(3.0 * _modelError / dtSeconds) / dtSeconds;
        var dt3 = (1.0 / 3.0) * dtSeconds * dtSeconds * dtSeconds * sigma * sigma;
        var dt2 = (1.0 / 2.0) * dtSeconds * dtSeconds * sigma * sigma;

        _kalman.ProcessNoiseCovariance[0, 0] = dt3;
        _kalman.ProcessNoiseCovariance[0, 1] = dt2;
        _kalman.ProcessNoiseCovariance[1, 0] = dt2;
        _kalman.ProcessNoiseCovariance[1, 1] = dtSeconds * sigma * sigma;
    }
}
using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Vision.Filter;

//  A position‑&‑velocity tracking filter:
//
//      state  x = [ px  py  vx  vy ]ᵀ
//      meas   z = [ px  py ]ᵀ
//
//  – constant‑velocity model
//  – white‑noise acceleration with tunable variance
public class Filter2D
{
    private readonly float _modelError;

    public DateTime LastTimestamp { get; private set; }

    private readonly KalmanFilter _kalman;

    // Initialize with pos-only measurement (x, y), zero velocity.
    public Filter2D(Vector2 initialPosition,
        float covariance, float modelError, float measurementError,
        DateTime timestamp)
    {
        _kalman = new KalmanFilter(4, 2, 1);

        // -- initial state
        Position = initialPosition;
        // leave v_x, v_y = 0

        // -- initial covariance
        PositionCovariance = covariance;
        VelocityCovariance = covariance;

        // -- measurement H = [1 0 0 0; 0 1 0 0]
        _kalman.MeasurementMatrix[0, 0] = 1.0;
        _kalman.MeasurementMatrix[1, 1] = 1.0;

        MeasurementError = measurementError;
        _modelError = modelError;

        LastTimestamp = timestamp;
    }

    // Initialize from full 4‑vector state.
    public Filter2D(Vector2 initialPosition, Vector2 initialVelocity,
        float covariance, float modelError, float measurementError,
        DateTime timestamp)
        : this(initialPosition, covariance, modelError, measurementError, timestamp)
    {
        Velocity = initialVelocity;
    }

    /// <summary>
    /// Predict forward to a new timestamp.
    /// </summary>
    public void Predict(DateTime timestamp)
    {
        var dt = (timestamp - LastTimestamp).TotalSeconds;
        if (dt <= 0) return;

        LastTimestamp = timestamp;
        UpdateMatrices(dt);
        _kalman.Predict();
    }

    /// <summary>
    /// Feed in a new (x,y) measurement.
    /// </summary>
    public void Correct(Vector2 position)
    {
        _kalman.Correct(position.AsMathNetVector());
    }

    public Vector2 Position
    {
        get => _kalman.StateEstimate.ToVector2();
        private set
        {
            _kalman.StateEstimate[0] = value.X;
            _kalman.StateEstimate[1] = value.Y;
        }
    }

    public Vector2 Velocity
    {
        get => _kalman.StateEstimate.ToVector2(2);
        private set
        {
            _kalman.StateEstimate[2] = value.X;
            _kalman.StateEstimate[3] = value.Y;
        }
    }

    public Vector2 GetPositionEstimate(DateTime timestamp)
    {
        var dt = (timestamp - LastTimestamp).TotalSeconds;
        return Position + Velocity * (float)dt;
    }

    public Vector2 PositionUncertainty => new(
        (float)Math.Sqrt(_kalman.ErrorCovariance[0, 0]),
        (float)Math.Sqrt(_kalman.ErrorCovariance[1, 1]));

    public Vector2 VelocityUncertainty => new(
        (float)Math.Sqrt(_kalman.ErrorCovariance[2, 2]),
        (float)Math.Sqrt(_kalman.ErrorCovariance[3, 3]));

    // length‑2 innovation = z – Hx
    public Vector2 PositionInnovation => _kalman.Innovation.ToVector2();

    private float MeasurementError
    {
        get => (float)_kalman.MeasurementNoiseCovariance[0, 0];
        set
        {
            _kalman.MeasurementNoiseCovariance[0, 0] = value;
            _kalman.MeasurementNoiseCovariance[1, 1] = value;
        }
    }

    public float PositionCovariance
    {
        get => (float)_kalman.ErrorCovariance[0, 0];
        set
        {
            _kalman.ErrorCovariance[0, 0] = value;
            _kalman.ErrorCovariance[1, 1] = value;
        }
    }

    public float VelocityCovariance
    {
        get => (float)_kalman.ErrorCovariance[2, 2];
        set
        {
            _kalman.ErrorCovariance[2, 2] = value;
            _kalman.ErrorCovariance[3, 3] = value;
        }
    }

    /// <summary>
    /// Build F and Q for constant‑vel model with white accel noise.
    /// </summary>
    private void UpdateMatrices(double dt)
    {
        _kalman.TransitionMatrix[0, 0] = 1;
        _kalman.TransitionMatrix[0, 2] = dt;

        _kalman.TransitionMatrix[1, 1] = 1;
        _kalman.TransitionMatrix[1, 3] = dt;

        _kalman.TransitionMatrix[2, 2] = 1;

        _kalman.TransitionMatrix[3, 3] = 1;

        // set optimal process noise σ² Q for white‑noise accel: see Simon, “Optimal State Estimation.”
        // here error = E[a²] * dt
        var sigma = Math.Sqrt(3.0 * _modelError / dt) / dt;
        var dt3 = (1.0 / 3.0) * dt * dt * dt * sigma * sigma;
        var dt2 = (1.0 / 2.0) * dt * dt * sigma * sigma;
        var dt1 = dt * sigma * sigma;

        _kalman.ProcessNoiseCovariance[0, 0] = dt3;
        _kalman.ProcessNoiseCovariance[0, 2] = dt2;

        _kalman.ProcessNoiseCovariance[1, 1] = dt3;
        _kalman.ProcessNoiseCovariance[1, 3] = dt2;

        _kalman.ProcessNoiseCovariance[2, 0] = dt2;
        _kalman.ProcessNoiseCovariance[2, 2] = dt1;

        _kalman.ProcessNoiseCovariance[3, 1] = dt2;
        _kalman.ProcessNoiseCovariance[3, 3] = dt1;
    }
}
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearRegression;
using Tyr.Common.Math.Shapes;

namespace Tyr.Common.Math;

/// <summary>
/// Estimates a line using least-squares regression over a rolling window of samples.
/// </summary>
public class LineEstimator
{
    /// <summary>
    /// Maximum number of samples stored in the estimator.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Current number of samples in the estimator.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// True if the sample buffer is full.
    /// </summary>
    public bool IsFull => Count == Capacity;

    /// <summary>
    /// True if the estimator has enough samples to compute an estimate.
    /// </summary>
    public bool IsReady => Count >= MinSamples;

    /// <summary>
    /// Returns the current line estimate, or null if not enough samples.
    /// Automatically recomputes when new data has been added.
    /// </summary>
    public Line? Estimate
    {
        get
        {
            if (_dirty) Compute();
            return _estimate;
        }
    }

    private const int MinSamples = 2;

    private int _index;
    private bool _dirty;

    private readonly Vector<double> _yVector;
    private readonly Matrix<double> _xMatrix;
    private Line? _estimate;

    static LineEstimator()
    {
        // Math.NET's default Qr solver uses parallel for,
        // which for small matrices has a negative performance impact.
        MathNet.Numerics.Control.UseSingleThread();
    }

    /// <summary>
    /// Creates a new line estimator with a given sample capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of samples to keep.</param>
    public LineEstimator(int capacity)
    {
        if (capacity < MinSamples) throw new ArgumentException($"Capacity must be >= {MinSamples}", nameof(capacity));
        Capacity = capacity;

        _yVector = Vector.Build.Dense(capacity, 0.0);
        _xMatrix = Matrix.Build.Dense(capacity, 2,
            (_, j) => j == 0 ? 1.0 : 0.0);
    }

    /// <summary>
    /// Adds a new (x, y) sample to the estimator.
    /// Older samples are overwritten in circular order when capacity is reached.
    /// </summary>
    public void AddSample(double x, double y)
    {
        _xMatrix[_index, 1] = x;
        _yVector[_index] = y;

        _index = (_index + 1) % Capacity;
        Count = System.Math.Min(Count + 1, Capacity);

        _dirty = true;
    }

    /// <summary>
    /// Clears all stored samples and resets the estimate.
    /// </summary>
    public void Reset()
    {
        Count = 0;
        _index = 0;
        _estimate = null;
        _dirty = false;
    }

    private void Compute()
    {
        // This ensures even failed computations don’t re-run unnecessarily unless new data is added.
        _dirty = false;

        if (!IsReady)
        {
            _estimate = null;
            return;
        }

        // These lines do not allocate when IsFull is true.
        var xMatrix = IsFull ? _xMatrix : _xMatrix.SubMatrix(0, Count, 0, 2);
        var yVector = IsFull ? _yVector : _yVector.SubVector(0, Count);

        try
        {
            var beta = MultipleRegression.QR(xMatrix, yVector);

            _estimate = Line.FromSlopeAndIntercept(
                slope: (float)beta[1],
                intercept: (float)beta[0]);
        }
        catch (Exception exception)
        {
            Log.ZLogError(exception, $"Failed to compute line estimate: {exception.Message}");
            _estimate = null;
        }
    }
}
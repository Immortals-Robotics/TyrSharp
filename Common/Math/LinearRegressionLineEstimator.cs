using Tyr.Common.Math.Shapes;

namespace Tyr.Common.Math;

/// <summary>
/// Estimates <c>y = mx + b</c> using ordinary least squares over a rolling window of samples.
/// This is appropriate when X is the independent variable and residual error is measured vertically.
/// </summary>
public class LinearRegressionLineEstimator
{
    public int Capacity { get; }
    public int Count { get; private set; }
    public bool IsFull => Count == Capacity;
    public bool IsReady => Count >= MinSamples;

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

    private readonly double[] _xSamples;
    private readonly double[] _ySamples;
    private Line? _estimate;

    public LinearRegressionLineEstimator(int capacity)
    {
        if (capacity < MinSamples) throw new ArgumentException($"Capacity must be >= {MinSamples}", nameof(capacity));
        Capacity = capacity;

        _xSamples = new double[capacity];
        _ySamples = new double[capacity];
    }

    public void AddSample(double x, double y)
    {
        _xSamples[_index] = x;
        _ySamples[_index] = y;

        _index = (_index + 1) % Capacity;
        Count = System.Math.Min(Count + 1, Capacity);

        _dirty = true;
    }

    public void Reset()
    {
        Count = 0;
        _index = 0;
        _estimate = null;
        _dirty = false;
    }

    private void Compute()
    {
        _dirty = false;

        if (!IsReady)
        {
            _estimate = null;
            return;
        }

        try
        {
            double meanX = 0.0;
            double meanY = 0.0;

            for (var i = 0; i < Count; i++)
            {
                meanX += _xSamples[i];
                meanY += _ySamples[i];
            }

            meanX /= Count;
            meanY /= Count;

            double varianceX = 0.0;
            double covarianceXY = 0.0;

            for (var i = 0; i < Count; i++)
            {
                var dx = _xSamples[i] - meanX;
                var dy = _ySamples[i] - meanY;

                varianceX += dx * dx;
                covarianceXY += dx * dy;
            }

            if (Utils.ApproximatelyZero((float)varianceX))
            {
                _estimate = null;
                return;
            }

            var slope = covarianceXY / varianceX;
            var intercept = meanY - slope * meanX;

            _estimate = Line.FromSlopeAndIntercept((float)slope, (float)intercept);
        }
        catch (Exception exception)
        {
            Log.ZLogError(exception, $"Failed to compute line estimate: {exception.Message}");
            _estimate = null;
        }
    }
}

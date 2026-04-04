using System.Numerics;
using Tyr.Common.Math.Shapes;

namespace Tyr.Common.Math;

/// <summary>
/// Estimates a geometric 2D line using orthogonal regression over a rolling window of samples.
/// This is appropriate when both X and Y contain measurement noise.
/// </summary>
public class OrthogonalRegressionLineEstimator
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

    public OrthogonalRegressionLineEstimator(int capacity)
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

            double covarianceXX = 0.0;
            double covarianceXY = 0.0;
            double covarianceYY = 0.0;

            for (var i = 0; i < Count; i++)
            {
                var dx = _xSamples[i] - meanX;
                var dy = _ySamples[i] - meanY;

                covarianceXX += dx * dx;
                covarianceXY += dx * dy;
                covarianceYY += dy * dy;
            }

            if (Utils.ApproximatelyZero((float)(covarianceXX + covarianceYY)))
            {
                _estimate = null;
                return;
            }

            Vector2 direction;
            if (Utils.ApproximatelyZero((float)covarianceXY))
            {
                direction = covarianceXX >= covarianceYY
                    ? Vector2.UnitX
                    : Vector2.UnitY;
            }
            else
            {
                var trace = covarianceXX + covarianceYY;
                var determinant = covarianceXX * covarianceYY - covarianceXY * covarianceXY;
                var halfTrace = trace * 0.5;
                var discriminant = System.Math.Sqrt(System.Math.Max(0.0, halfTrace * halfTrace - determinant));
                var largestEigenvalue = halfTrace + discriminant;

                direction = Vector2.Normalize(
                    new Vector2((float)covarianceXY, (float)(largestEigenvalue - covarianceXX)));
            }

            if (Utils.ApproximatelyZero(direction.LengthSquared()))
            {
                _estimate = null;
                return;
            }

            var center = new Vector2((float)meanX, (float)meanY);
            _estimate = Line.FromTwoPoints(center, center + direction);
        }
        catch (Exception exception)
        {
            Log.ZLogError(exception, $"Failed to compute line estimate: {exception.Message}");
            _estimate = null;
        }
    }
}

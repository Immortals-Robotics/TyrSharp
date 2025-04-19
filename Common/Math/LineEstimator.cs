using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Tyr.Common.Shapes;

namespace Tyr.Common.Math;

public class LineEstimator
{
    public Line? Estimate { get; private set; }

    public int Capacity { get; }
    public int Count { get; private set; }
    public bool IsFull => Count == Capacity;

    private int _index;

    private readonly Matrix<double> _x;
    private readonly Vector<double> _y;

    public LineEstimator(int capacity)
    {
        if (capacity <= 0) throw new ArgumentException("Capacity must be positive", nameof(capacity));
        Capacity = capacity;

        _x = Matrix.Build.Dense(capacity, 2,
            (_, j) => j == 0 ? 1.0 : 0.0);

        _y = Vector.Build.Dense(capacity, 0.0);
    }

    public void AddSample(double x, double y)
    {
        _x[_index, 1] = x;
        _y[_index] = y;

        _index = (_index + 1) % Capacity;
        Count = System.Math.Min(Count + 1, Capacity);

        Compute();
    }

    public void Reset()
    {
        Count = 0;
        _index = 0;
        Estimate = null;
    }

    private void Compute()
    {
        if (Count < 2)
        {
            Estimate = null;
            return;
        }

        var x = IsFull ? _x : _x.SubMatrix(0, Count, 0, 2);
        var y = IsFull ? _y : _y.SubVector(0, Count);

        try
        {
            var qr = x.QR();
            
            var qTy = qr.Q.TransposeThisAndMultiply(y);
            var beta = qr.R.UpperTriangle().Solve(qTy);

            Estimate = Line.FromSlopeAndIntercept(
                slope: (float)beta[1],
                intercept: (float)beta[0]);
        }
        catch (Exception exception)
        {
            Logger.ZLogError(exception, $"Failed to compute line estimate: {exception.Message}");
            Estimate = null;
        }
    }
}
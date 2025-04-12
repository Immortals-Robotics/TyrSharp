using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace Tyr.Common.Math;

public class LineEstimator
{
    public (double slope, double intercept)? Estimate;

    public int Capacity { get; }
    public int Count { get; private set; }
    public bool IsFull => Count == Capacity;

    private int _index;

    private readonly Matrix<double> _x;
    private readonly Vector<double> _y;

    public LineEstimator(int capacity)
    {
        Capacity = capacity;

        _x = Matrix.Build.Dense(capacity, 2,
            (_, j) => j == 0 ? 1f : 0f);

        _y = Vector.Build.Dense(capacity, 0f);
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

        var qr = x.QR();
        var qTy = qr.Q.TransposeThisAndMultiply(y);
        var beta = qr.R.UpperTriangle().Solve(qTy);

        Estimate = (slope: beta[1], intercept: beta[0]);
    }
}
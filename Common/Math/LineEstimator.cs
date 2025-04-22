using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Tyr.Common.Math.Shapes;

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

        var xMatrix = IsFull ? _x : _x.SubMatrix(0, Count, 0, 2);
        var yVector = IsFull ? _y : _y.SubVector(0, Count);

        try
        {
            // Extract x values (second column)
            var xVector = xMatrix.Column(1);

            // Compute means
            var meanX = xVector.Average();
            var meanY = yVector.Average();

            // Center x and y
            var xCentered = xVector - meanX;
            var yCentered = yVector - meanY;

            // Build design matrix: column of 1s + x'
            var ones = Vector<double>.Build.Dense(Count, 1.0);
            var design = Matrix.Build.DenseOfColumnVectors(ones, xCentered);

            var qr = design.QR();

            var qTy = qr.Q.TransposeThisAndMultiply(yCentered);
            var beta = qr.R.UpperTriangle().Solve(qTy);

            var slope = beta[1];
            var intercept = meanY + beta[0] - slope * meanX;

            Estimate = Line.FromSlopeAndIntercept(
                slope: (float)slope,
                intercept: (float)intercept);
        }
        catch (Exception exception)
        {
            Logger.ZLogError(exception, $"Failed to compute line estimate: {exception.Message}");
            Estimate = null;
        }
    }
}
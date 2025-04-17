using System.Runtime.CompilerServices;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Assertion;

public partial class Assert
{
    public void IsNormalized(Vector2 v,
        [CallerArgumentExpression("v")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || Utils.ApproximatelyEqual(v.Length(), 1f)) return;
        logger.ZLogError($"Expected normalized vector: {expr} (length = {v.Length():0.###})", null, member, file, line);
    }

    public void HasNoNaNs(Vector2 v,
        [CallerArgumentExpression("v")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || !(float.IsNaN(v.X) || float.IsNaN(v.Y))) return;
        logger.ZLogError($"Vector {expr} contains NaNs: ({v.X}, {v.Y})", null, member, file, line);
    }

    public void IsOrthogonal(Vector2 a, Vector2 b,
        [CallerArgumentExpression("a")] string? aExpr = null,
        [CallerArgumentExpression("b")] string? bExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var dot = a.Dot(b);
        if (!Enabled || Utils.ApproximatelyEqual(dot, 0f)) return;
        logger.ZLogError($"Expected orthogonal vectors: {aExpr} · {bExpr} = {dot:0.###}", null, member, file, line);
    }

    public void IsParallel(Vector2 a, Vector2 b,
        [CallerArgumentExpression("a")] string? aExpr = null,
        [CallerArgumentExpression("b")] string? bExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var cross = a.Cross(b);
        if (!Enabled || Utils.ApproximatelyEqual(cross, 0f)) return;
        logger.ZLogError($"Expected parallel vectors: {aExpr} × {bExpr} = {cross:0.###}", null, member, file, line);
    }
}
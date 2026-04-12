using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Assertion;

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public partial class Assert
{
    internal static bool CheckIsNormalized(Vector2 v) => Utils.ApproximatelyEqual(v.Length(), 1f);
    internal static bool CheckHasNoNaNs(Vector2 v) => !(float.IsNaN(v.X) || float.IsNaN(v.Y));
    internal static bool CheckIsOrthogonal(float dot) => Utils.ApproximatelyZero(dot);
    internal static bool CheckIsParallel(float cross) => Utils.ApproximatelyZero(cross);

    internal void ReportIsNormalized(Vector2 v, string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected normalized vector: {expr} (length = {v.Length():0.###})", null, member, file, line);

    internal void ReportHasNoNaNs(Vector2 v, string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Vector {expr} contains NaNs: ({v.X}, {v.Y})", null, member, file, line);

    internal void ReportIsOrthogonal(float dot, string? aExpr, string? bExpr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected orthogonal vectors: {aExpr} · {bExpr} = {dot:0.###}", null, member, file, line);

    internal void ReportIsParallel(float cross, string? aExpr, string? bExpr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected parallel vectors: {aExpr} × {bExpr} = {cross:0.###}", null, member, file, line);
}

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public readonly partial struct AssertProxy
{
    public void IsNormalized(Vector2 v,
        [CallerArgumentExpression("v")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsNormalized(v)) return;
        Resolve().ReportIsNormalized(v, expr, member, file, line);
    }

    public void HasNoNaNs(Vector2 v,
        [CallerArgumentExpression("v")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckHasNoNaNs(v)) return;
        Resolve().ReportHasNoNaNs(v, expr, member, file, line);
    }

    public void IsOrthogonal(Vector2 a, Vector2 b,
        [CallerArgumentExpression("a")] string? aExpr = null,
        [CallerArgumentExpression("b")] string? bExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var dot = Vector2.Dot(a, b);
        if (!Assert.Enabled || Assert.CheckIsOrthogonal(dot)) return;
        Resolve().ReportIsOrthogonal(dot, aExpr, bExpr, member, file, line);
    }

    public void IsParallel(Vector2 a, Vector2 b,
        [CallerArgumentExpression("a")] string? aExpr = null,
        [CallerArgumentExpression("b")] string? bExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var cross = a.Cross(b);
        if (!Assert.Enabled || Assert.CheckIsParallel(cross)) return;
        Resolve().ReportIsParallel(cross, aExpr, bExpr, member, file, line);
    }
}

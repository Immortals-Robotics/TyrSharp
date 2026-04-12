using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Assertion;

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public partial class Assert
{
    internal static bool CheckAreApproximatelyEqual(float expected, float actual, float tolerance = 0.001f) =>
        Utils.ApproximatelyEqual(expected, actual, tolerance);

    internal static bool CheckAreNotApproximatelyEqual(float expected, float actual, float tolerance = 0.001f) =>
        !Utils.ApproximatelyEqual(expected, actual, tolerance);

    internal static bool CheckIsZero(float value) => value == 0;
    internal static bool CheckIsApproximatelyZero(float value, float tolerance = 0.001f) =>
        Utils.ApproximatelyZero(value, tolerance);
    internal static bool CheckIsPositive(float value) => value > 0;
    internal static bool CheckIsNegative(float value) => value < 0;
    internal static bool CheckIsFinite(float value) => !float.IsInfinity(value) && !float.IsNaN(value);
    internal static bool CheckIsNotNaN(float value) => !float.IsNaN(value);
    internal static bool CheckInRange(float value, float min, float max) => value >= min && value <= max;

    internal void ReportAreApproximatelyEqual(float expected, float actual, float tolerance, string? expectedExpr,
        string? actualExpr, string? member, string? file, int line) =>
        logger.ZLogError(
            $"AreApproximatelyEqual failed: {expectedExpr} ({expected}) ≠ {actualExpr} ({actual}) | tolerance: {tolerance}",
            null, member, file, line);

    internal void ReportAreNotApproximatelyEqual(float expected, float actual, float tolerance, string? expectedExpr,
        string? actualExpr, string? member, string? file, int line) =>
        logger.ZLogError(
            $"AreNotApproximatelyEqual failed: {expectedExpr} ({expected}) ≈ {actualExpr} ({actual}) | tolerance: {tolerance}",
            null, member, file, line);

    internal void ReportIsZero(float value, string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected zero: {expr} ({value})", null, member, file, line);

    internal void ReportIsApproximatelyZero(float value, float tolerance, string? expr, string? member, string? file,
        int line) =>
        logger.ZLogError($"Expected ~zero: {expr} ({value})  | tolerance: {tolerance}", null, member, file, line);

    internal void ReportIsPositive(float value, string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected positive value: {expr} ({value})", null, member, file, line);

    internal void ReportIsNegative(float value, string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected negative value: {expr} ({value})", null, member, file, line);

    internal void ReportIsFinite(float value, string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected finite number: {expr} ({value})", null, member, file, line);

    internal void ReportIsNotNaN(string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected non-NaN value: {expr}", null, member, file, line);

    internal void ReportInRange(float value, float min, float max, string? expr, string? member, string? file,
        int line) =>
        logger.ZLogError($"Expected {expr} to be in range [{min}, {max}], but was {value}", null, member, file, line);
}

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public readonly partial struct AssertProxy
{
    public void AreApproximatelyEqual(float expected, float actual, float tolerance = 0.001f,
        [CallerArgumentExpression("expected")] string? expectedExpr = null,
        [CallerArgumentExpression("actual")] string? actualExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckAreApproximatelyEqual(expected, actual, tolerance)) return;
        Resolve().ReportAreApproximatelyEqual(expected, actual, tolerance, expectedExpr, actualExpr, member, file, line);
    }

    public void AreNotApproximatelyEqual(float expected, float actual, float tolerance = 0.001f,
        [CallerArgumentExpression("expected")] string? expectedExpr = null,
        [CallerArgumentExpression("actual")] string? actualExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckAreNotApproximatelyEqual(expected, actual, tolerance)) return;
        Resolve().ReportAreNotApproximatelyEqual(expected, actual, tolerance, expectedExpr, actualExpr, member, file,
            line);
    }

    public void IsZero(float value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsZero(value)) return;
        Resolve().ReportIsZero(value, expr, member, file, line);
    }

    public void IsApproximatelyZero(float value, float tolerance = 0.001f,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsApproximatelyZero(value, tolerance)) return;
        Resolve().ReportIsApproximatelyZero(value, tolerance, expr, member, file, line);
    }

    public void IsPositive(float value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsPositive(value)) return;
        Resolve().ReportIsPositive(value, expr, member, file, line);
    }

    public void IsNegative(float value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsNegative(value)) return;
        Resolve().ReportIsNegative(value, expr, member, file, line);
    }

    public void IsFinite(float value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsFinite(value)) return;
        Resolve().ReportIsFinite(value, expr, member, file, line);
    }

    public void IsNotNaN(float value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsNotNaN(value)) return;
        Resolve().ReportIsNotNaN(expr, member, file, line);
    }

    public void InRange(float value, float min, float max,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckInRange(value, min, max)) return;
        Resolve().ReportInRange(value, min, max, expr, member, file, line);
    }
}

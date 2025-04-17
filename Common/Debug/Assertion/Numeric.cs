using System.Runtime.CompilerServices;
using Tyr.Common.Math;

namespace Tyr.Common.Debug.Assertion;

public partial class Assert
{
    public void AreApproximatelyEqual(float expected, float actual, float tolerance = 0.001f,
        [CallerArgumentExpression("expected")] string? expectedExpr = null,
        [CallerArgumentExpression("actual")] string? actualExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || Utils.ApproximatelyEqual(expected, actual, tolerance)) return;
        logger.ZLogError(
            $"AreApproximatelyEqual failed: {expectedExpr} ({expected}) ≠ {actualExpr} ({actual}) | tolerance: {tolerance}",
            null, member, file, line);
    }

    public void AreNotApproximatelyEqual(float expected, float actual, float tolerance = 0.001f,
        [CallerArgumentExpression("expected")] string? expectedExpr = null,
        [CallerArgumentExpression("actual")] string? actualExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || !Utils.ApproximatelyEqual(expected, actual, tolerance)) return;
        logger.ZLogError(
            $"AreNotApproximatelyEqual failed: {expectedExpr} ({expected}) ≈ {actualExpr} ({actual}) | tolerance: {tolerance}",
            null, member, file, line);
    }

    public void IsZero(float value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || value == 0) return;
        logger.ZLogError($"Expected zero: {expr} ({value})", null, member, file, line);
    }

    public void IsApproximatelyZero(float value, float tolerance = 0.001f,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || Utils.ApproximatelyEqual(value, 0f, tolerance)) return;
        logger.ZLogError($"Expected ~zero: {expr} ({value})  | tolerance: {tolerance}", null, member, file, line);
    }

    public void IsPositive(float value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || value > 0) return;
        logger.ZLogError($"Expected positive value: {expr} ({value})", null, member, file, line);
    }

    public void IsNegative(float value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || value < 0) return;
        logger.ZLogError($"Expected negative value: {expr} ({value})", null, member, file, line);
    }

    public void IsFinite(float value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || !float.IsInfinity(value) && !float.IsNaN(value)) return;
        logger.ZLogError($"Expected finite number: {expr} ({value})", null, member, file, line);
    }

    public void IsNotNaN(float value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || !float.IsNaN(value)) return;
        logger.ZLogError($"Expected non-NaN value: {expr}", null, member, file, line);
    }

    public void InRange(float value, float min, float max,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || (value >= min && value <= max)) return;
        logger.ZLogError($"Expected {expr} to be in range [{min}, {max}], but was {value}", null, member, file, line);
    }
}
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Tyr.Common.Config;
using Tyr.Common.Math;

namespace Tyr.Common.Debug;

[Configurable]
public class Assert(ILogger logger)
{
    [ConfigEntry] private static bool Enabled { get; } = true;

    public void IsTrue(bool condition,
        [CallerArgumentExpression("condition")]
        string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || condition) return;
        logger.ZLogError($"Expected true: {expr}", null, member, file, line);
    }


    public void IsFalse(bool condition,
        [CallerArgumentExpression("condition")]
        string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || !condition) return;
        logger.ZLogError($"Expected false: {expr}", null, member, file, line);
    }


    public void IsNull(object? value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || value == null) return;
        logger.ZLogError($"Expected null: {expr}", null, member, file, line);
    }


    public void IsNotNull(object? value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || value != null) return;
        logger.ZLogError($"Expected not null: {expr}", null, member, file, line);
    }


    public void AreEqual<T>(T expected, T actual,
        [CallerArgumentExpression("expected")] string? expectedExpr = null,
        [CallerArgumentExpression("actual")] string? actualExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || EqualityComparer<T>.Default.Equals(expected, actual)) return;
        logger.ZLogError($"AreEqual failed: {expectedExpr} ({expected}) != {actualExpr} ({actual})", null,
            member, file, line);
    }


    public void AreNotEqual<T>(T notExpected, T actual,
        [CallerArgumentExpression("notExpected")]
        string? notExpectedExpr = null,
        [CallerArgumentExpression("actual")] string? actualExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || !EqualityComparer<T>.Default.Equals(notExpected, actual)) return;
        logger.ZLogError($"AreNotEqual failed: {notExpectedExpr} ({notExpected}) == {actualExpr} ({actual})", null,
            member, file, line);
    }


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
}
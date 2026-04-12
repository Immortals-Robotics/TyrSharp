using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug.Assertion;

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public partial class Assert
{
    internal static bool CheckAreEqual<T>(T expected, T actual) => EqualityComparer<T>.Default.Equals(expected, actual);
    internal static bool CheckAreNotEqual<T>(T notExpected, T actual) => !EqualityComparer<T>.Default.Equals(notExpected, actual);

    internal void ReportAreEqual<T>(T expected, T actual, string? expectedExpr, string? actualExpr, string? member,
        string? file, int line) =>
        logger.ZLogError($"AreEqual failed: {expectedExpr} ({expected}) != {actualExpr} ({actual})", null,
            member, file, line);

    internal void ReportAreNotEqual<T>(T notExpected, T actual, string? notExpectedExpr, string? actualExpr,
        string? member, string? file, int line) =>
        logger.ZLogError($"AreNotEqual failed: {notExpectedExpr} ({notExpected}) == {actualExpr} ({actual})", null,
            member, file, line);
}

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public readonly partial struct AssertProxy
{
    public void AreEqual<T>(T expected, T actual,
        [CallerArgumentExpression("expected")] string? expectedExpr = null,
        [CallerArgumentExpression("actual")] string? actualExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckAreEqual(expected, actual)) return;
        Resolve().ReportAreEqual(expected, actual, expectedExpr, actualExpr, member, file, line);
    }

    public void AreNotEqual<T>(T notExpected, T actual,
        [CallerArgumentExpression("notExpected")] string? notExpectedExpr = null,
        [CallerArgumentExpression("actual")] string? actualExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckAreNotEqual(notExpected, actual)) return;
        Resolve().ReportAreNotEqual(notExpected, actual, notExpectedExpr, actualExpr, member, file, line);
    }
}

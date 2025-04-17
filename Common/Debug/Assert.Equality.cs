using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug;

public partial class Assert
{
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
}
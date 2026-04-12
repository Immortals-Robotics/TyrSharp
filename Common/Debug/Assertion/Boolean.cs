using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug.Assertion;

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public partial class Assert
{
    internal static bool CheckIsTrue(bool condition) => condition;
    internal static bool CheckIsFalse(bool condition) => !condition;

    internal void ReportIsTrue(string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected true: {expr}", null, member, file, line);

    internal void ReportIsFalse(string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected false: {expr}", null, member, file, line);
}

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public readonly partial struct AssertProxy
{
    public void IsTrue(bool condition,
        [CallerArgumentExpression("condition")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsTrue(condition)) return;
        Resolve().ReportIsTrue(expr, member, file, line);
    }

    public void IsFalse(bool condition,
        [CallerArgumentExpression("condition")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsFalse(condition)) return;
        Resolve().ReportIsFalse(expr, member, file, line);
    }
}

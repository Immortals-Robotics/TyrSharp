using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug.Assertion;

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public partial class Assert
{
    internal static bool CheckIsType<TExpected>(object? obj) => obj is TExpected;
    internal static bool CheckImplements<TInterface>(Type? type) => type != null && typeof(TInterface).IsAssignableFrom(type);
    internal static bool CheckHasSameReference(object? a, object? b) => ReferenceEquals(a, b);
    internal static bool CheckHasSameType(object? a, object? b) => a?.GetType() == b?.GetType();

    internal void ReportIsType<TExpected>(object? obj, string? expr, string? member, string? file, int line) =>
        logger.ZLogError(
            $"Expected {expr} to be of type {typeof(TExpected).Name}, but was {obj?.GetType().Name ?? "null"}",
            null, member, file, line);

    internal void ReportImplements<TInterface>(string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected {expr} to implement {typeof(TInterface).Name}, but it did not",
            null, member, file, line);

    internal void ReportHasSameReference(string? aExpr, string? bExpr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected reference equality: {aExpr} and {bExpr} are not the same object",
            null, member, file, line);

    internal void ReportHasSameType(object? a, object? b, string? aExpr, string? bExpr, string? member, string? file,
        int line) =>
        logger.ZLogError(
            $"Expected {aExpr} and {bExpr} to be of same type, but were {a?.GetType().Name ?? "null"} and {b?.GetType().Name ?? "null"}",
            null, member, file, line);
}

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public readonly partial struct AssertProxy
{
    public void IsType<TExpected>(object? obj,
        [CallerArgumentExpression("obj")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsType<TExpected>(obj)) return;
        Resolve().ReportIsType<TExpected>(obj, expr, member, file, line);
    }

    public void Implements<TInterface>(Type? type,
        [CallerArgumentExpression("type")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckImplements<TInterface>(type)) return;
        Resolve().ReportImplements<TInterface>(expr, member, file, line);
    }

    public void HasSameReference(object? a, object? b,
        [CallerArgumentExpression("a")] string? aExpr = null,
        [CallerArgumentExpression("b")] string? bExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckHasSameReference(a, b)) return;
        Resolve().ReportHasSameReference(aExpr, bExpr, member, file, line);
    }

    public void HasSameType(object? a, object? b,
        [CallerArgumentExpression("a")] string? aExpr = null,
        [CallerArgumentExpression("b")] string? bExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckHasSameType(a, b)) return;
        Resolve().ReportHasSameType(a, b, aExpr, bExpr, member, file, line);
    }
}

using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug;

public partial class Assert
{
    public void IsType<TExpected>(object? obj,
        [CallerArgumentExpression("obj")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || obj is TExpected) return;
        logger.ZLogError(
            $"Expected {expr} to be of type {typeof(TExpected).Name}, but was {obj?.GetType().Name ?? "null"}",
            null, member, file, line);
    }

    public void Implements<TInterface>(Type? type,
        [CallerArgumentExpression("type")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || (type != null && typeof(TInterface).IsAssignableFrom(type))) return;
        logger.ZLogError($"Expected {expr} to implement {typeof(TInterface).Name}, but it did not",
            null, member, file, line);
    }

    public void HasSameReference(object? a, object? b,
        [CallerArgumentExpression("a")] string? aExpr = null,
        [CallerArgumentExpression("b")] string? bExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || ReferenceEquals(a, b)) return;
        logger.ZLogError($"Expected reference equality: {aExpr} and {bExpr} are not the same object",
            null, member, file, line);
    }

    public void HasSameType(object? a, object? b,
        [CallerArgumentExpression("a")] string? aExpr = null,
        [CallerArgumentExpression("b")] string? bExpr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || (a?.GetType() == b?.GetType())) return;
        logger.ZLogError(
            $"Expected {aExpr} and {bExpr} to be of same type, but were {a?.GetType().Name ?? "null"} and {b?.GetType().Name ?? "null"}",
            null, member, file, line);
    }
}
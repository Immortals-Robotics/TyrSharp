using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug.Assertion;

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public partial class Assert
{
    internal static bool CheckIsNull(object? value) => value == null;
    internal static bool CheckIsNotNull(object? value) => value != null;
    internal static bool CheckHasValue<T>(T? value) where T : struct => value.HasValue;

    internal void ReportIsNull(string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected null: {expr}", null, member, file, line);

    internal void ReportIsNotNull(string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected not null: {expr}", null, member, file, line);

    internal void ReportHasValue<T>(string? expr, string? member, string? file, int line) where T : struct =>
        logger.ZLogError($"Expected {expr} to have a value (Nullable<{typeof(T).Name}> was null)",
            null, member, file, line);
}

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public readonly partial struct AssertProxy
{
    public void IsNull(object? value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsNull(value)) return;
        Resolve().ReportIsNull(expr, member, file, line);
    }

    public void IsNotNull(object? value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsNotNull(value)) return;
        Resolve().ReportIsNotNull(expr, member, file, line);
    }

    public void HasValue<T>(T? value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0) where T : struct
    {
        if (!Assert.Enabled || Assert.CheckHasValue(value)) return;
        Resolve().ReportHasValue<T>(expr, member, file, line);
    }
}

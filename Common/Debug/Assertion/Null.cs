using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug.Assertion;

public partial class Assert
{
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

    public void HasValue<T>(T? value,
        [CallerArgumentExpression("value")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0) where T : struct
    {
        if (!Enabled || value.HasValue) return;
        logger.ZLogError($"Expected {expr} to have a value (Nullable<{typeof(T).Name}> was null)",
            null, member, file, line);
    }
}
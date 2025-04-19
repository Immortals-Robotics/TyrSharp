using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug.Assertion;

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public partial class Assert
{
    public void IsEmpty<T>(IEnumerable<T> collection,
        [CallerArgumentExpression("collection")]
        string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || !collection.Any()) return;
        logger.ZLogError($"Expected {expr} to be empty", null, member, file, line);
    }

    public void IsNotEmpty<T>(IEnumerable<T> collection,
        [CallerArgumentExpression("collection")]
        string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || collection.Any()) return;
        logger.ZLogError($"Expected {expr} to be non-empty", null, member, file, line);
    }

    public void Contains<T>(IEnumerable<T> collection, T item,
        [CallerArgumentExpression("collection")]
        string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || collection.Contains(item)) return;
        logger.ZLogError($"Expected {expr} to contain item '{item}'", null, member, file, line);
    }

    public void DoesNotContain<T>(IEnumerable<T> collection, T item,
        [CallerArgumentExpression("collection")]
        string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || !collection.Contains(item)) return;
        logger.ZLogError($"Expected {expr} to not contain item '{item}'", null, member, file, line);
    }

    public void All<T>(IEnumerable<T> collection, Func<T, bool> predicate,
        [CallerArgumentExpression("collection")]
        string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || collection.All(predicate)) return;
        logger.ZLogError($"Expected all elements in {expr} to match predicate, but at least one did not", null, member,
            file, line);
    }

    public void Any<T>(IEnumerable<T> collection, Func<T, bool> predicate,
        [CallerArgumentExpression("collection")]
        string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || collection.Any(predicate)) return;
        logger.ZLogError($"Expected at least one element in {expr} to match predicate, but none did", null, member,
            file, line);
    }
}
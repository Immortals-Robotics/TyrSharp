using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug.Assertion;

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public partial class Assert
{
    internal static bool CheckIsEmpty<T>(IEnumerable<T> collection) => !collection.Any();
    internal static bool CheckIsNotEmpty<T>(IEnumerable<T> collection) => collection.Any();
    internal static bool CheckContains<T>(IEnumerable<T> collection, T item) => collection.Contains(item);
    internal static bool CheckDoesNotContain<T>(IEnumerable<T> collection, T item) => !collection.Contains(item);
    internal static bool CheckContainsKey<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key) => dictionary.ContainsKey(key);
    internal static bool CheckDoesNotContainKey<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key) => !dictionary.ContainsKey(key);
    internal static bool CheckAll<T>(IEnumerable<T> collection, Func<T, bool> predicate) => collection.All(predicate);
    internal static bool CheckAny<T>(IEnumerable<T> collection, Func<T, bool> predicate) => collection.Any(predicate);

    internal void ReportIsEmpty(string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected {expr} to be empty", null, member, file, line);

    internal void ReportIsNotEmpty(string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected {expr} to be non-empty", null, member, file, line);

    internal void ReportContains<T>(T item, string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected {expr} to contain item '{item}'", null, member, file, line);

    internal void ReportDoesNotContain<T>(T item, string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected {expr} to not contain item '{item}'", null, member, file, line);

    internal void ReportContainsKey<TKey>(TKey key, string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected {expr} to contain key '{key}'", null, member, file, line);

    internal void ReportDoesNotContainKey<TKey>(TKey key, string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected {expr} to not contain key '{key}'", null, member, file, line);

    internal void ReportAll(string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected all elements in {expr} to match predicate, but at least one did not", null, member,
            file, line);

    internal void ReportAny(string? expr, string? member, string? file, int line) =>
        logger.ZLogError($"Expected at least one element in {expr} to match predicate, but none did", null, member,
            file, line);
}

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public readonly partial struct AssertProxy
{
    public void IsEmpty<T>(IEnumerable<T> collection,
        [CallerArgumentExpression("collection")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsEmpty(collection)) return;
        Resolve().ReportIsEmpty(expr, member, file, line);
    }

    public void IsNotEmpty<T>(IEnumerable<T> collection,
        [CallerArgumentExpression("collection")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckIsNotEmpty(collection)) return;
        Resolve().ReportIsNotEmpty(expr, member, file, line);
    }

    public void Contains<T>(IEnumerable<T> collection, T item,
        [CallerArgumentExpression("collection")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckContains(collection, item)) return;
        Resolve().ReportContains(item, expr, member, file, line);
    }

    public void DoesNotContain<T>(IEnumerable<T> collection, T item,
        [CallerArgumentExpression("collection")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckDoesNotContain(collection, item)) return;
        Resolve().ReportDoesNotContain(item, expr, member, file, line);
    }

    public void Contains<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key,
        [CallerArgumentExpression("dictionary")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckContainsKey(dictionary, key)) return;
        Resolve().ReportContainsKey(key, expr, member, file, line);
    }

    public void DoesNotContain<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key,
        [CallerArgumentExpression("dictionary")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckDoesNotContainKey(dictionary, key)) return;
        Resolve().ReportDoesNotContainKey(key, expr, member, file, line);
    }

    public void All<T>(IEnumerable<T> collection, Func<T, bool> predicate,
        [CallerArgumentExpression("collection")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckAll(collection, predicate)) return;
        Resolve().ReportAll(expr, member, file, line);
    }

    public void Any<T>(IEnumerable<T> collection, Func<T, bool> predicate,
        [CallerArgumentExpression("collection")] string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Assert.Enabled || Assert.CheckAny(collection, predicate)) return;
        Resolve().ReportAny(expr, member, file, line);
    }
}

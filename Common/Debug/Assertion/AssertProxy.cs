using System.Diagnostics.CodeAnalysis;

namespace Tyr.Common.Debug.Assertion;

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public readonly partial struct AssertProxy(Assert ownedAssert, Func<Assert> resolver)
{
    private Assert Resolve()
    {
        var resolved = resolver();
        return ReferenceEquals(resolved, ownedAssert) ? ownedAssert : resolved;
    }
}

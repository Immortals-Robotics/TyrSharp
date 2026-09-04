using System.Diagnostics.CodeAnalysis;

namespace Tyr.Common.Debug.Assertion;

[SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
public readonly partial struct AssertProxy(Func<Assert> resolver)
{
    private Assert Resolve() => resolver();
}

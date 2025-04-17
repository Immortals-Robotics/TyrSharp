using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug;

public partial class Assert
{
    public void IsTrue(bool condition,
        [CallerArgumentExpression("condition")]
        string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || condition) return;
        logger.ZLogError($"Expected true: {expr}", null, member, file, line);
    }


    public void IsFalse(bool condition,
        [CallerArgumentExpression("condition")]
        string? expr = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        if (!Enabled || !condition) return;
        logger.ZLogError($"Expected false: {expr}", null, member, file, line);
    }
}
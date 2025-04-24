namespace Tyr.Common.Debug;

public static class ModuleContext
{
    public static readonly AsyncLocal<string?> Current = new();
}
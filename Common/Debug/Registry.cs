using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Tyr.Common.Debug.Assertion;
using Tyr.Common.Debug.Drawing;

namespace Tyr.Common.Debug;

public static class Registry
{
    private static readonly ConcurrentDictionary<string, ILogger> Loggers = [];
    private static readonly ConcurrentDictionary<string, Assert> Asserts = [];
    private static readonly ConcurrentDictionary<string, Drawer> Drawers = [];

    public static ILogger GetLogger(string moduleName) => Loggers.GetOrAdd(moduleName, Log.Factory.CreateLogger);

    public static Assert GetAssert(string moduleName) =>
        Asserts.GetOrAdd(moduleName, name => new Assert(GetLogger(name)));

    public static Drawer GetDrawer(string moduleName) =>
        Drawers.GetOrAdd(moduleName, name => new Drawer(name));
}
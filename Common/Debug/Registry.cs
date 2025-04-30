using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Tyr.Common.Debug.Assertion;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Plotting;

namespace Tyr.Common.Debug;

public static class Registry
{
    private static readonly ConcurrentDictionary<string, ILogger> Loggers = [];
    private static readonly ConcurrentDictionary<string, Assert> Asserts = [];
    private static readonly ConcurrentDictionary<string, Drawer> Drawers = [];
    private static readonly ConcurrentDictionary<string, Plotter> Plotters = [];

    private static readonly Func<string, ILogger> LoggerFactory = Logging.Logging.Factory.CreateLogger;
    private static readonly Func<string, Assert> AssertFactory = name => new Assert(GetLogger(name));
    private static readonly Func<string, Drawer> DrawerFactory = name => new Drawer(name);
    private static readonly Func<string, Plotter> PlotterFactory = name => new Plotter(name);

    public static ILogger GetLogger(string moduleName) => Loggers.GetOrAdd(moduleName, LoggerFactory);

    public static Assert GetAssert(string moduleName) => Asserts.GetOrAdd(moduleName, AssertFactory);

    public static Drawer GetDrawer(string moduleName) => Drawers.GetOrAdd(moduleName, DrawerFactory);

    public static Plotter GetPlotter(string moduleName) => Plotters.GetOrAdd(moduleName, PlotterFactory);
}
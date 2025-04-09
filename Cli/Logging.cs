global using static Tyr.Cli.Logging;
using Microsoft.Extensions.Logging;

namespace Tyr.Cli;

internal static class Logging
{
    public static readonly ILogger Logger = Common.Debug.Log.GetLogger("Cli");
}
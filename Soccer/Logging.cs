global using static Tyr.Soccer.Logging;
global using ZLogger;

using Microsoft.Extensions.Logging;

namespace Tyr.Soccer;

internal static class Logging
{
    public static readonly ILogger Logger = Common.Debug.Log.GetLogger("Soccer");
}
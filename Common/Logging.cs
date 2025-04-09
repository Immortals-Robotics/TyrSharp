global using static Tyr.Common.Logging;
global using ZLogger;

using Microsoft.Extensions.Logging;

namespace Tyr.Common;

internal static class Logging
{
    public static readonly ILogger Logger = Debug.Log.GetLogger("Common");
}
global using static Tyr.Referee.Logging;
global using ZLogger;

using Microsoft.Extensions.Logging;

namespace Tyr.Referee;

internal static class Logging
{
    public static readonly ILogger Logger = Common.Debug.Log.GetLogger("Referee");
}
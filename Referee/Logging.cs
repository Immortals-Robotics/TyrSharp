global using static Tyr.Referee.Logging;
using Microsoft.Extensions.Logging;

namespace Tyr.Referee;

internal static class Logging
{
    public static readonly ILogger Logger = Common.Debug.Log.GetLogger("Referee");
}
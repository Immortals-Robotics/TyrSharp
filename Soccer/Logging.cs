global using static Tyr.Soccer.Logging;
using Microsoft.Extensions.Logging;

namespace Tyr.Soccer;

internal static class Logging
{
    public static readonly ILogger Logger = Common.Debug.Log.GetLogger("Soccer");
}
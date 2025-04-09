global using static Tyr.Vision.Logging;
using Microsoft.Extensions.Logging;

namespace Tyr.Vision;

internal static class Logging
{
    public static readonly ILogger Logger = Common.Debug.Log.GetLogger("Vision");
}
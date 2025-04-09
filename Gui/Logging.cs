global using static Tyr.Gui.Logging;
using Microsoft.Extensions.Logging;

namespace Tyr.Gui;

internal static class Logging
{
    public static readonly ILogger Logger = Common.Debug.Log.GetLogger("Gui");
}
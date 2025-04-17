using Microsoft.Extensions.Logging;
using Tyr.Common.Config;

namespace Tyr.Common.Debug;

[Configurable]
public partial class Assert(ILogger logger)
{
    [ConfigEntry] private static bool Enabled { get; } = true;
}
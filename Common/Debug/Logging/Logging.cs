using System.Globalization;
using Microsoft.Extensions.Logging;
using Tyr.Common.Config;
using ZLogger.Providers;

namespace Tyr.Common.Debug.Logging;

[Configurable]
public static partial class Logging
{
    [ConfigEntry("The level logs are filtered at the source. Anything below this will be rejected on the log call.")]
    private static partial LogLevel Level { get; set; } = LogLevel.Debug;

    [ConfigEntry("The minimum level written to the console/stdout sink. Anything below this will still be available to other log sinks.")]
    private static partial LogLevel ConsoleLevel { get; set; } = LogLevel.Information;

    static Logging()
    {
        // some cultures especially in europe use ',' instead of '.' for the decimal point
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }

    internal static ILoggerFactory Factory { get; set; } = LoggerFactory.Create(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Trace);
        logging.AddFilter(level => level >= Level);
        logging.AddFilter<ZLoggerConsoleLoggerProvider>(level => level >= ConsoleLevel);
        logging.AddZLoggerConsole(options =>
        {
            options.UsePlainTextFormatter(formatter =>
            {
                formatter.SetPrefixFormatter($"[{0} | {1} | {2} | {3} @ {4}:{5}] ",
                    (in template, in info) =>
                    {
                        template.Format(
                            info.Timestamp, info.Category, info.LogLevel,
                            info.MemberName, PathCache.GetFileName(info.FilePath), info.LineNumber);
                    });
            });
        });

        logging.AddZLoggerLogProcessor(options =>
        {
            options.UsePlainTextFormatter();
            return new LogPublisher();
        });
    });
}

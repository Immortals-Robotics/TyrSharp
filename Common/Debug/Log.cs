using System.Globalization;
using Microsoft.Extensions.Logging;
using Tyr.Common.Config;

namespace Tyr.Common.Debug;

[Configurable]
public static class Log
{
    [ConfigEntry] private static LogLevel Level { get; set; } = LogLevel.Trace;

    static Log()
    {
        // some cultures especially in europe use ',' instead of '.' for the decimal point 
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }

    private static ILoggerFactory Factory { get; set; } = LoggerFactory.Create(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Trace);
        logging.AddFilter(level => level >= Level);
        logging.AddZLoggerConsole(options =>
        {
            options.CaptureThreadInfo = true;
            options.UsePlainTextFormatter(formatter =>
            {
                formatter.SetPrefixFormatter($"[{0} | {1} | {2} | {3} @ {4}:{5}] ",
                    (in MessageTemplate template, in LogInfo info) =>
                    {
                        template.Format(
                            info.Timestamp, info.Category, info.LogLevel,
                            info.MemberName, Path.GetFileName(info.FilePath), info.LineNumber);
                    });
            });
        });
    });

    public static ILogger GetLogger(string moduleName) => Factory.CreateLogger(moduleName);
}
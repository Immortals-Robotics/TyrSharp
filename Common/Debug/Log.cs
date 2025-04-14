using System.Globalization;
using Microsoft.Extensions.Logging;
using Tyr.Common.Config;

namespace Tyr.Common.Debug;

[Configurable]
public static class Log
{
    // TODO: changes to these are not picked up due to init order issues
    [ConfigEntry] private static LogLevel Level { get; set; } = LogLevel.Trace;

    static Log()
    {
        // some cultures especially in europe use ',' instead of '.' for the decimal point 
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }

    private static ILoggerFactory Factory { get; set; } = LoggerFactory.Create(logging =>
    {
        logging.SetMinimumLevel(Level);
        logging.AddZLoggerConsole(options =>
        {
            options.CaptureThreadInfo = true;
            options.UsePlainTextFormatter(formatter =>
            {
                formatter.SetPrefixFormatter($"[{0:timeonly} | {1} | {2} | {3} @ {4}:{5}] ",
                    (in MessageTemplate template, in LogInfo info) =>
                    {
                        template.Format(
                            info.Timestamp, info.Category, info.LogLevel,
                            info.MemberName, Path.GetFileName(info.FilePath), info.LineNumber);
                    });
            });
        });
    });

    public static ILogger<T> GetLogger<T>() where T : class => Factory.CreateLogger<T>();
    public static ILogger GetLogger(string name) => Factory.CreateLogger(name);
}
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Tyr.Common.Debug;

public static partial class Debug
{
    private static ILoggerFactory Factory { get; set; } = null!;
    public static ILogger Logger { get; private set; } = null!;

    public static void InitLogging(LogLevel level)
    {
        Factory = LoggerFactory.Create(logging =>
        {
            logging.SetMinimumLevel(level);
            logging.AddZLoggerConsole(options =>
            {
                options.CaptureThreadInfo = true;
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter($"[{0:timeonly} | {1} | {2} @ {3}:{4}] ",
                        (in MessageTemplate template, in LogInfo info) =>
                        {
                            template.Format(
                                info.Timestamp, info.LogLevel,
                                info.MemberName, Path.GetFileName(info.FilePath), info.LineNumber);
                        });
                });
            });
        });

        Logger = Factory.CreateLogger("Tyr");
    }

    // standard LoggerFactory caches logger per category so no need to cache in this manager
    public static ILogger<T> GetLogger<T>() where T : class => Factory.CreateLogger<T>();
    public static ILogger GetLogger(string categoryName) => Factory.CreateLogger(categoryName);
}
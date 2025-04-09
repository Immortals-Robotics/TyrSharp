using Microsoft.Extensions.Logging;
using ZLogger;

namespace Tyr.Common.Debug;

public static class Log
{
    private static ILoggerFactory Factory { get; set; } = LoggerFactory.Create(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Trace);
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
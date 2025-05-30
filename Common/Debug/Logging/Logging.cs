﻿using System.Globalization;
using Microsoft.Extensions.Logging;
using Tyr.Common.Config;

namespace Tyr.Common.Debug.Logging;

[Configurable]
public static partial class Logging
{
    [ConfigEntry("The level logs are filtered at the source. Anything below this will be rejected on the log call.")]
    private static LogLevel Level { get; set; } = LogLevel.Debug;

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
        logging.AddZLoggerConsole(options =>
        {
            options.UsePlainTextFormatter(formatter =>
            {
                formatter.SetPrefixFormatter($"[{0} | {1} | {2} | {3} @ {4}:{5}] ",
                    (in MessageTemplate template, in LogInfo info) =>
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
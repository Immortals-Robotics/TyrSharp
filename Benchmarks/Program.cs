using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Db;
using Tyr.Common.Debug.Logging;
using Tyr.Common.Debug.Plotting;
using Tyr.Common.Time;
using DrawCommand = Tyr.Common.Debug.Drawing.Command;
using LoggingEntry = Tyr.Common.Debug.Logging.Entry;
using PlotCommand = Tyr.Common.Debug.Plotting.Command;

var config = BenchmarkConfig.FromArgs(args);
var scenarios = BenchmarkScenario.CreateScenarios(config);

Console.WriteLine("DebugDb Benchmarks");
Console.WriteLine($"Date: {DateTimeOffset.Now:O}");
Console.WriteLine($"Framework: {Environment.Version}");
Console.WriteLine($"Warmup iterations: {config.WarmupIterations}");
Console.WriteLine($"Measured iterations: {config.MeasurementIterations}");
Console.WriteLine($"Append entry count: {config.AppendEntryCount:N0}");
Console.WriteLine($"Query entry count: {config.QueryEntryCount:N0}");
Console.WriteLine($"Shard count: {config.ShardCount:N0}");
Console.WriteLine($"Parallel workers: {config.ParallelWorkers}");
Console.WriteLine($"Sample result counts: {string.Join(", ", config.SampleResultCounts.Select(count => count.ToString("N0")))}");
Console.WriteLine($"Real-world modules: {config.ModuleCount:N0}");
Console.WriteLine($"Real-world frames/module: {config.FrameCount:N0}");
Console.WriteLine($"Real-world per-frame mix: logs={config.LogsPerFrame:N0}, draws={config.DrawsPerFrame:N0}, plots={config.PlotIdsPerModule:N0}");
Console.WriteLine($"Real-world open plots/module: {config.OpenPlotsPerModule:N0}");
Console.WriteLine($"Real-world plot max points: {config.PlotWindowMaxPoints:N0}");
Console.WriteLine();

foreach (var scenario in scenarios)
{
    Console.WriteLine($"Scenario: {scenario.Name}");
    Console.WriteLine($"Load: {scenario.Description}");

    for (var i = 0; i < config.WarmupIterations; i++)
        scenario.Run();

    var samples = new List<ScenarioResult>(config.MeasurementIterations);
    for (var i = 0; i < config.MeasurementIterations; i++)
        samples.Add(scenario.Run());

    PrintSummary(samples);
    Console.WriteLine();
}

static void PrintSummary(List<ScenarioResult> samples)
{
    var meanDurationMs = samples.Average(sample => sample.Duration.TotalMilliseconds);
    var minDurationMs = samples.Min(sample => sample.Duration.TotalMilliseconds);
    var maxDurationMs = samples.Max(sample => sample.Duration.TotalMilliseconds);
    var meanThroughput = samples.Average(sample => sample.Operations / sample.Duration.TotalSeconds);

    Console.WriteLine($"Mean: {meanDurationMs:N2} ms");
    Console.WriteLine($"Min:  {minDurationMs:N2} ms");
    Console.WriteLine($"Max:  {maxDurationMs:N2} ms");
    Console.WriteLine($"Throughput: {meanThroughput:N0} ops/s");

    if (samples.All(sample => sample.ResultCount.HasValue))
        Console.WriteLine($"Result count: {samples[0].ResultCount!.Value:N0}");
}

internal sealed record BenchmarkConfig(
    int WarmupIterations,
    int MeasurementIterations,
    int AppendEntryCount,
    int QueryEntryCount,
    int ShardCount,
    int ParallelWorkers,
    int[] SampleResultCounts,
    int ModuleCount,
    int FrameCount,
    int LogsPerFrame,
    int DrawsPerFrame,
    int PlotIdsPerModule,
    int OpenPlotsPerModule,
    int PlotWindowMaxPoints)
{
    public static BenchmarkConfig FromArgs(string[] args)
    {
        var values = args
            .Select(arg => arg.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && parts[0].StartsWith("--", StringComparison.Ordinal))
            .ToDictionary(parts => parts[0][2..], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        return new BenchmarkConfig(
            WarmupIterations: GetInt(values, "warmup", 1),
            MeasurementIterations: GetInt(values, "iterations", 5),
            AppendEntryCount: GetInt(values, "append-count", 200_000),
            QueryEntryCount: GetInt(values, "query-count", 250_000),
            ShardCount: GetInt(values, "shards", 16),
            ParallelWorkers: GetInt(values, "workers", Math.Max(2, Environment.ProcessorCount / 2)),
            SampleResultCounts: GetIntList(values, "sample-counts", [100, 1_000, 10_000]),
            ModuleCount: GetInt(values, "modules", 6),
            FrameCount: GetInt(values, "frames", 600),
            LogsPerFrame: GetInt(values, "logs-per-frame", 8),
            DrawsPerFrame: GetInt(values, "draws-per-frame", 24),
            PlotIdsPerModule: GetInt(values, "plots-per-module", 24),
            OpenPlotsPerModule: GetInt(values, "open-plots", 4),
            PlotWindowMaxPoints: GetInt(values, "plot-max-points", 500));
    }

    private static int GetInt(Dictionary<string, string> values, string key, int fallback)
    {
        return values.TryGetValue(key, out var raw) && int.TryParse(raw, out var value)
            ? value
            : fallback;
    }

    private static int[] GetIntList(Dictionary<string, string> values, string key, int[] fallback)
    {
        if (!values.TryGetValue(key, out var raw))
            return fallback;

        var parsed = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var value) ? value : -1)
            .Where(value => value > 0)
            .Distinct()
            .Order()
            .ToArray();

        return parsed.Length > 0 ? parsed : fallback;
    }
}

internal readonly record struct ScenarioResult(TimeSpan Duration, long Operations, int? ResultCount);

internal sealed class BenchmarkScenario
{
    public string Name { get; }
    public string Description { get; }
    private readonly Func<ScenarioResult> _run;

    private BenchmarkScenario(string name, string description, Func<ScenarioResult> run)
    {
        Name = name;
        Description = description;
        _run = run;
    }

    public ScenarioResult Run() => _run();

    public static IReadOnlyList<BenchmarkScenario> CreateScenarios(BenchmarkConfig config)
    {
        var scenarios = new List<BenchmarkScenario>
        {
            new BenchmarkScenario(
                "Append.Sequential.SingleShard",
                $"{config.AppendEntryCount:N0} log entries into one module/source/shard",
                () => RunAppendSequentialSingleShard(config)),
            new BenchmarkScenario(
                "Append.Sequential.ManyShards",
                $"{config.AppendEntryCount:N0} plot entries spread across {config.ShardCount:N0} shard keys",
                () => RunAppendSequentialManyShards(config)),
            new BenchmarkScenario(
                "Append.Parallel.SharedShard",
                $"{config.AppendEntryCount:N0} log entries across {config.ParallelWorkers} workers into one hot shard",
                () => RunAppendParallelSharedShard(config)),
            new BenchmarkScenario(
                "Append.MixedPlayback.RealWorld",
                $"{config.ModuleCount:N0} modules x {config.FrameCount:N0} frames with logs, draws, plots, and frames",
                () => RunAppendMixedPlaybackRealWorld(config)),
            new BenchmarkScenario(
                "Query.All.SingleShard",
                $"{config.QueryEntryCount:N0} plot entries queried from one shard",
                () => RunQueryAllSingleShard(config)),
            new BenchmarkScenario(
                "Query.All.MultiShard",
                $"{config.QueryEntryCount:N0} plot entries queried across {config.ShardCount:N0} shards",
                () => RunQueryAllMultiShard(config)),
            new BenchmarkScenario(
                "Query.LogView.RealWorld",
                "Per-module frame lookup followed by log queries for the active playback time",
                () => RunLogViewRealWorld(config)),
            new BenchmarkScenario(
                "Query.FieldView.RealWorld",
                "Per-module frame lookup followed by draw queries for the active playback time",
                () => RunFieldViewRealWorld(config)),
            new BenchmarkScenario(
                "Query.PlotCatalog.RealWorld",
                "Plot sidebar metadata discovery via shard listing and one-row plot lookup",
                () => RunPlotCatalogRealWorld(config)),
            new BenchmarkScenario(
                "Query.PlotWindow.RealWorld",
                $"{config.OpenPlotsPerModule:N0} open plots/module sampled to {config.PlotWindowMaxPoints:N0} points",
                () => RunPlotWindowRealWorld(config)),
            new BenchmarkScenario(
                "Query.DebugFilter.RealWorld",
                "Debug filter tree build via per-module source-location scans",
                () => RunDebugFilterRealWorld(config)),
        };

        foreach (var sampleCount in config.SampleResultCounts)
        {
            scenarios.Add(new BenchmarkScenario(
                $"Query.Sampled.SingleShard.{sampleCount:N0}",
                $"{config.QueryEntryCount:N0} plot entries sampled to {sampleCount:N0} results from one shard",
                () => RunQuerySampledSingleShard(config, sampleCount)));

            scenarios.Add(new BenchmarkScenario(
                $"Query.Sampled.MultiShard.{sampleCount:N0}",
                $"{config.QueryEntryCount:N0} plot entries sampled to {sampleCount:N0} results across {config.ShardCount:N0} shards",
                () => RunQuerySampledMultiShard(config, sampleCount)));
        }

        return scenarios;
    }

    private static ScenarioResult RunAppendSequentialSingleShard(BenchmarkConfig config)
    {
        return WithTempDb(db =>
        {
            db.RegisterType<LoggingEntry>();

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < config.AppendEntryCount; i++)
                db.Append(CreateLogEntry(i, module: "Vision", message: $"message-{i}"));
            stopwatch.Stop();

            return new ScenarioResult(stopwatch.Elapsed, config.AppendEntryCount, null);
        });
    }

    private static ScenarioResult RunAppendSequentialManyShards(BenchmarkConfig config)
    {
        return WithTempDb(db =>
        {
            db.RegisterType<PlotCommand>();

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < config.AppendEntryCount; i++)
                db.Append(CreatePlotEntry(i, module: "Vision", shardKey: $"robot-{i % config.ShardCount}"));
            stopwatch.Stop();

            return new ScenarioResult(stopwatch.Elapsed, config.AppendEntryCount, null);
        });
    }

    private static ScenarioResult RunAppendParallelSharedShard(BenchmarkConfig config)
    {
        return WithTempDb(db =>
        {
            db.RegisterType<LoggingEntry>();

            var entriesPerWorker = config.AppendEntryCount / config.ParallelWorkers;
            var remainder = config.AppendEntryCount % config.ParallelWorkers;

            var stopwatch = Stopwatch.StartNew();
            Parallel.For(0, config.ParallelWorkers, workerIndex =>
            {
                var localCount = entriesPerWorker + (workerIndex < remainder ? 1 : 0);
                var start = workerIndex * entriesPerWorker + Math.Min(workerIndex, remainder);
                for (var i = 0; i < localCount; i++)
                {
                    var ordinal = start + i;
                    db.Append(CreateLogEntry(ordinal, module: "Vision", message: $"parallel-{ordinal}"));
                }
            });
            stopwatch.Stop();

            return new ScenarioResult(stopwatch.Elapsed, config.AppendEntryCount, null);
        });
    }

    private static ScenarioResult RunAppendMixedPlaybackRealWorld(BenchmarkConfig config)
    {
        return WithTempDb(db =>
        {
            db.RegisterType<LoggingEntry>()
                .RegisterType<DrawCommand>()
                .RegisterType<PlotCommand>();

            var stopwatch = Stopwatch.StartNew();
            var dataset = PopulateRealWorldDataset(db, config);
            stopwatch.Stop();

            return new ScenarioResult(stopwatch.Elapsed, dataset.AppendedOperations, null);
        });
    }

    private static ScenarioResult RunQueryAllSingleShard(BenchmarkConfig config)
    {
        return RunQueryScenario(
            config,
            populate: db =>
            {
                db.RegisterType<PlotCommand>();
                for (var i = 0; i < config.QueryEntryCount; i++)
                    db.Append(CreatePlotEntry(i, module: "Vision", shardKey: "ball"));
            },
            query: db =>
            {
                var stopwatch = Stopwatch.StartNew();
                var resultCount = db.Query<PlotCommand>("Vision", Timestamp.Zero, Timestamp.MaxValue, "ball").Count();
                stopwatch.Stop();
                return new ScenarioResult(stopwatch.Elapsed, config.QueryEntryCount, resultCount);
            });
    }

    private static ScenarioResult RunQueryAllMultiShard(BenchmarkConfig config)
    {
        return RunQueryScenario(
            config,
            populate: db =>
            {
                db.RegisterType<PlotCommand>();
                for (var i = 0; i < config.QueryEntryCount; i++)
                    db.Append(CreatePlotEntry(i, module: "Vision", shardKey: $"robot-{i % config.ShardCount}"));
            },
            query: db =>
            {
                var stopwatch = Stopwatch.StartNew();
                var resultCount = db.QueryAll<PlotCommand>(Timestamp.Zero, Timestamp.MaxValue).Count();
                stopwatch.Stop();
                return new ScenarioResult(stopwatch.Elapsed, config.QueryEntryCount, resultCount);
            });
    }

    private static ScenarioResult RunQuerySampledSingleShard(BenchmarkConfig config, int sampleCount)
    {
        return RunQueryScenario(
            config,
            populate: db =>
            {
                db.RegisterType<PlotCommand>();
                for (var i = 0; i < config.QueryEntryCount; i++)
                    db.Append(CreatePlotEntry(i, module: "Vision", shardKey: "ball"));
            },
            query: db =>
            {
                var stopwatch = Stopwatch.StartNew();
                var resultCount = db.Query<PlotCommand>("Vision", Timestamp.Zero, Timestamp.MaxValue, "ball", sampleCount).Count();
                stopwatch.Stop();
                return new ScenarioResult(stopwatch.Elapsed, sampleCount, resultCount);
            });
    }

    private static ScenarioResult RunQuerySampledMultiShard(BenchmarkConfig config, int sampleCount)
    {
        return RunQueryScenario(
            config,
            populate: db =>
            {
                db.RegisterType<PlotCommand>();
                for (var i = 0; i < config.QueryEntryCount; i++)
                    db.Append(CreatePlotEntry(i, module: "Vision", shardKey: $"robot-{i % config.ShardCount}"));
            },
            query: db =>
            {
                var stopwatch = Stopwatch.StartNew();
                var resultCount = db.QueryAll<PlotCommand>(Timestamp.Zero, Timestamp.MaxValue, maxCount: sampleCount).Count();
                stopwatch.Stop();
                return new ScenarioResult(stopwatch.Elapsed, sampleCount, resultCount);
            });
    }

    private static ScenarioResult RunLogViewRealWorld(BenchmarkConfig config)
    {
        return RunRealWorldQueryScenario(config, static (db, dataset) =>
        {
            var resultCount = 0;

            var stopwatch = Stopwatch.StartNew();
            foreach (var module in db.QueryModules())
            {
                var frame = db.GetFrameAt(module, dataset.FocusTime);
                if (!frame.HasValue)
                    continue;

                resultCount += db.Query<LoggingEntry>(module, frame.Value.Start, frame.Value.End).Count();
            }
            stopwatch.Stop();

            return new ScenarioResult(stopwatch.Elapsed, resultCount, resultCount);
        });
    }

    private static ScenarioResult RunFieldViewRealWorld(BenchmarkConfig config)
    {
        return RunRealWorldQueryScenario(config, static (db, dataset) =>
        {
            var resultCount = 0;

            var stopwatch = Stopwatch.StartNew();
            foreach (var module in db.QueryModules())
            {
                var frame = db.GetFrameAt(module, dataset.FocusTime);
                if (!frame.HasValue)
                    continue;

                resultCount += db.Query<DrawCommand>(module, frame.Value.Start, frame.Value.End).Count();
            }
            stopwatch.Stop();

            return new ScenarioResult(stopwatch.Elapsed, resultCount, resultCount);
        });
    }

    private static ScenarioResult RunPlotCatalogRealWorld(BenchmarkConfig config)
    {
        return RunRealWorldQueryScenario(config, static (db, dataset) =>
        {
            var lookupCount = 0;
            var resultCount = 0;

            var stopwatch = Stopwatch.StartNew();
            foreach (var module in db.QueryModules())
            {
                foreach (var plotId in db.QueryShardKeys<PlotCommand>(module))
                {
                    lookupCount++;
                    if (db.TryGetShardMeta<PlotCommand>(module, plotId).HasValue)
                        resultCount++;
                }
            }
            stopwatch.Stop();

            return new ScenarioResult(stopwatch.Elapsed, lookupCount, resultCount);
        });
    }

    private static ScenarioResult RunPlotWindowRealWorld(BenchmarkConfig config)
    {
        return RunRealWorldQueryScenario(config, (db, dataset) =>
        {
            var resultCount = 0;

            var stopwatch = Stopwatch.StartNew();
            foreach (var module in dataset.Modules)
            {
                foreach (var plotId in dataset.PlotIdsByModule[module].Take(config.OpenPlotsPerModule))
                {
                    resultCount += db.Query<PlotCommand>(
                        module,
                        dataset.WindowStart,
                        dataset.WindowEnd,
                        plotId,
                        config.PlotWindowMaxPoints).Count();
                }
            }
            stopwatch.Stop();

            return new ScenarioResult(stopwatch.Elapsed, resultCount, resultCount);
        });
    }

    private static ScenarioResult RunDebugFilterRealWorld(BenchmarkConfig config)
    {
        return RunRealWorldQueryScenario(config, static (db, _) =>
        {
            var metaCount = 0;

            var stopwatch = Stopwatch.StartNew();
            foreach (var module in db.QueryModules())
            {
                metaCount += db.QuerySourceLocations<LoggingEntry>(module).Count();
                metaCount += db.QuerySourceLocations<DrawCommand>(module).Count();
                metaCount += db.QuerySourceLocations<PlotCommand>(module).Count();
            }
            stopwatch.Stop();

            return new ScenarioResult(stopwatch.Elapsed, metaCount, metaCount);
        });
    }

    private static ScenarioResult RunQueryScenario(
        BenchmarkConfig config,
        Action<DebugDb> populate,
        Func<DebugDb, ScenarioResult> query)
    {
        var directory = CreateTempDirectory();
        try
        {
            using (var db = new DebugDb(directory))
                populate(db);

            ScenarioResult result;
            using (var reopened = new DebugDb(directory).RegisterType<PlotCommand>())
                result = query(reopened);

            return result;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ScenarioResult RunRealWorldQueryScenario(
        BenchmarkConfig config,
        Func<DebugDb, RealWorldDataset, ScenarioResult> query)
    {
        var directory = CreateTempDirectory();
        try
        {
            RealWorldDataset dataset;
            using (var db = CreateRegisteredDb(directory))
                dataset = PopulateRealWorldDataset(db, config);

            ScenarioResult result;
            using (var reopened = CreateRegisteredDb(directory))
                result = query(reopened, dataset);

            return result;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ScenarioResult WithTempDb(Func<DebugDb, ScenarioResult> run)
    {
        var directory = CreateTempDirectory();
        try
        {
            ScenarioResult result;
            using (var db = new DebugDb(directory))
                result = run(db);

            return result;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DebugDb CreateRegisteredDb(string directory)
    {
        return new DebugDb(directory)
            .RegisterType<LoggingEntry>()
            .RegisterType<DrawCommand>()
            .RegisterType<PlotCommand>();
    }

    private static RealWorldDataset PopulateRealWorldDataset(DebugDb db, BenchmarkConfig config)
    {
        var modules = Enumerable.Range(0, config.ModuleCount)
            .Select(index => $"Module{index + 1}")
            .ToArray();

        var plotIdsByModule = modules.ToDictionary(
            module => module,
            _ => Enumerable.Range(0, config.PlotIdsPerModule)
                .Select(index => $"signal-{index + 1}")
                .ToArray(),
            StringComparer.Ordinal);

        long timestampNs = 0;
        long appendedOperations = 0;
        long minFrameTimestamp = long.MaxValue;
        long maxFrameTimestamp = 0;

        for (var frameIndex = 0; frameIndex < config.FrameCount; frameIndex++)
        {
            for (var moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                var module = modules[moduleIndex];
                timestampNs += 1_000_000;
                var frameTimestamp = timestampNs;

                db.AppendFrame(new Frame
                {
                    ModuleName = module,
                    StartTimestamp = Timestamp.FromNanoseconds(frameTimestamp),
                });
                appendedOperations++;

                minFrameTimestamp = Math.Min(minFrameTimestamp, frameTimestamp);
                maxFrameTimestamp = Math.Max(maxFrameTimestamp, frameTimestamp);

                for (var logIndex = 0; logIndex < config.LogsPerFrame; logIndex++)
                {
                    db.Append(CreateRealWorldLogEntry(module, frameIndex, logIndex, frameTimestamp + 10 + logIndex));
                    appendedOperations++;
                }

                for (var drawIndex = 0; drawIndex < config.DrawsPerFrame; drawIndex++)
                {
                    db.Append(CreateRealWorldDrawEntry(module, frameIndex, drawIndex, frameTimestamp + 1_000 + drawIndex));
                    appendedOperations++;
                }

                var plotIds = plotIdsByModule[module];
                for (var plotIndex = 0; plotIndex < plotIds.Length; plotIndex++)
                {
                    db.Append(CreateRealWorldPlotEntry(
                        module,
                        plotIds[plotIndex],
                        frameIndex,
                        plotIndex,
                        frameTimestamp + 10_000 + plotIndex));
                    appendedOperations++;
                }
            }
        }

        var focusTimestamp = minFrameTimestamp + (maxFrameTimestamp - minFrameTimestamp) * 3 / 4;
        var windowLengthNs = Math.Max(1L, (long)(config.FrameCount / 6.0) * config.ModuleCount * 1_000_000L);
        var windowEnd = Math.Min(maxFrameTimestamp, focusTimestamp);
        var windowStart = Math.Max(minFrameTimestamp, windowEnd - windowLengthNs);

        return new RealWorldDataset(
            Modules: modules,
            PlotIdsByModule: plotIdsByModule,
            FocusTime: Timestamp.FromNanoseconds(focusTimestamp),
            WindowStart: Timestamp.FromNanoseconds(windowStart),
            WindowEnd: Timestamp.FromNanoseconds(windowEnd),
            AppendedOperations: appendedOperations);
    }

    private static LoggingEntry CreateLogEntry(int index, string module, string message)
    {
        return new LoggingEntry
        {
            Message = message,
            Level = LogLevel.Information,
            Meta = Meta.GetOrCreate(module, layer: "Benchmarks", file: "Program.cs", member: nameof(CreateLogEntry), line: 1),
            Timestamp = Timestamp.FromNanoseconds(index),
        };
    }

    private static LoggingEntry CreateRealWorldLogEntry(string module, int frameIndex, int logIndex, long timestampNs)
    {
        var layer = (logIndex % 3) switch
        {
            0 => "Runner",
            1 => "Strategy",
            _ => Meta.DebugLayer("Diagnostics"),
        };

        return new LoggingEntry
        {
            Message = $"{module} frame={frameIndex} log={logIndex}",
            Level = (logIndex % 5) switch
            {
                0 => LogLevel.Debug,
                1 => LogLevel.Information,
                2 => LogLevel.Warning,
                3 => LogLevel.Error,
                _ => LogLevel.Trace,
            },
            Meta = Meta.GetOrCreate(
                module,
                layer: layer,
                file: $"{module}.Runner.cs",
                member: logIndex % 2 == 0 ? "Tick" : "Update",
                line: 100 + logIndex,
                expression: null),
            Timestamp = Timestamp.FromNanoseconds(timestampNs),
        };
    }

    private static DrawCommand CreateRealWorldDrawEntry(string module, int frameIndex, int drawIndex, long timestampNs)
    {
        var layer = (drawIndex % 4) switch
        {
            0 => "Field",
            1 => "Pathing",
            2 => Meta.DebugLayer("Tactics"),
            _ => "Vision",
        };

        return new DrawCommand
        {
            Drawable = new Tyr.Common.Debug.Drawing.Drawables.Point
            {
                Position = new Vector2(frameIndex % 200, drawIndex % 100),
            },
            Color = default,
            Options = default,
            Meta = Meta.GetOrCreate(
                module,
                layer: layer,
                file: $"{module}.Field.cs",
                member: drawIndex % 2 == 0 ? "DrawWorldState" : "DrawPlan",
                line: 200 + drawIndex % 12,
                expression: $"point_{drawIndex % 8}"),
            Timestamp = Timestamp.FromNanoseconds(timestampNs),
        };
    }

    private static PlotCommand CreatePlotEntry(int index, string module, string shardKey)
    {
        return new PlotCommand
        {
            Id = shardKey,
            Title = "speed",
            Value = PlotValue.From(new Vector3(index, index + 1, index + 2)),
            Meta = Meta.GetOrCreate(module, layer: "Benchmarks", file: "Program.cs", member: nameof(CreatePlotEntry), line: 1),
            Timestamp = Timestamp.FromNanoseconds(index),
        };
    }

    private static PlotCommand CreateRealWorldPlotEntry(string module, string plotId, int frameIndex, int plotIndex, long timestampNs)
    {
        var layer = (plotIndex % 4) switch
        {
            0 => "Control",
            1 => "Vision",
            2 => "Strategy",
            _ => Meta.DebugLayer("Planner"),
        };

        return new PlotCommand
        {
            Id = plotId,
            Title = (plotIndex % 3) switch
            {
                0 => "value",
                1 => "velocity",
                _ => "error",
            },
            Value = plotIndex % 2 == 0
                ? PlotValue.From(frameIndex * 0.1 + plotIndex)
                : PlotValue.From(new Vector3(frameIndex, plotIndex, frameIndex + plotIndex)),
            Meta = Meta.GetOrCreate(
                module,
                layer: layer,
                file: $"{module}.Plots.cs",
                member: plotIndex % 2 == 0 ? "PublishTelemetry" : "PublishPlannerPlots",
                line: 300 + plotIndex % 16,
                expression: plotId),
            Timestamp = Timestamp.FromNanoseconds(timestampNs),
        };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TyrSharp.DebugDbBenchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed record RealWorldDataset(
        string[] Modules,
        Dictionary<string, string[]> PlotIdsByModule,
        Timestamp FocusTime,
        Timestamp WindowStart,
        Timestamp WindowEnd,
        long AppendedOperations);
}

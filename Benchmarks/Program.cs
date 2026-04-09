using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Db;
using Tyr.Common.Debug.Logging;
using Tyr.Common.Debug.Plotting;
using Tyr.Common.Time;
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
    int ParallelWorkers)
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
            ParallelWorkers: GetInt(values, "workers", Math.Max(2, Environment.ProcessorCount / 2)));
    }

    private static int GetInt(Dictionary<string, string> values, string key, int fallback)
    {
        return values.TryGetValue(key, out var raw) && int.TryParse(raw, out var value)
            ? value
            : fallback;
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
        return
        [
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
                "Query.All.SingleShard",
                $"{config.QueryEntryCount:N0} plot entries queried from one shard",
                () => RunQueryAllSingleShard(config)),
            new BenchmarkScenario(
                "Query.All.MultiShard",
                $"{config.QueryEntryCount:N0} plot entries queried across {config.ShardCount:N0} shards",
                () => RunQueryAllMultiShard(config)),
            new BenchmarkScenario(
                "Query.Sampled.MultiShard",
                $"{config.QueryEntryCount:N0} plot entries sampled to 1,000 results across {config.ShardCount:N0} shards",
                () => RunQuerySampledMultiShard(config)),
        ];
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

    private static ScenarioResult RunQuerySampledMultiShard(BenchmarkConfig config)
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
                const int maxCount = 1_000;
                var stopwatch = Stopwatch.StartNew();
                var resultCount = db.QueryAll<PlotCommand>(Timestamp.Zero, Timestamp.MaxValue, maxCount: maxCount).Count();
                stopwatch.Stop();
                return new ScenarioResult(stopwatch.Elapsed, maxCount, resultCount);
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

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TyrSharp.DebugDbBenchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}

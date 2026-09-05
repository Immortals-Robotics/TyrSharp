using Tomlet.Models;
using Tyr.Common.Config;
using Tyr.Common.Debug.Db;
using Tyr.Common.Time;
using Tyr.Control.GameController;
using Tyr.Control.Simulator;

namespace Tyr.Cli;

/// <summary>
/// Composes the headless stack and, when a scenario is given, drives grSim and the game controller
/// through it while recording the world. Exit codes: 0 ok, 2 a dependency was not ready in time,
/// 3 the scenario itself is invalid, 1 anything unexpected.
/// </summary>
public static class Harness
{
    private const int ExitOk = 0;
    private const int ExitError = 1;
    private const int ExitNotReady = 2;
    private const int ExitBadScenario = 3;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    public static int Run(HarnessOptions options, CancellationToken cancellation)
    {
        Scenario? scenario = null;
        if (options.ScenarioPath is not null)
        {
            try
            {
                scenario = Scenario.Load(options.ScenarioPath);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or Tomlet.Exceptions.TomlException)
            {
                Log.ZLogError($"Invalid scenario {options.ScenarioPath}: {ex.Message}");
                return ExitBadScenario;
            }

            if (options.DurationSecondsOverride is { } seconds)
            {
                scenario = new Scenario
                {
                    Name = scenario.Name,
                    Description = scenario.Description,
                    GrSim = scenario.GrSim,
                    Gc = scenario.Gc,
                    Config = scenario.Config,
                    Setup = scenario.Setup,
                    Duration = DeltaTime.FromSeconds((float)seconds),
                    SampleHz = scenario.SampleHz,
                    ReadinessTimeout = scenario.ReadinessTimeout,
                };
            }
        }

        var label = scenario?.Name ?? "headless";
        var runDirectory = CreateRunDirectory(options.OutputRoot, label);
        Log.ZLogInformation($"Run directory: {runDirectory}");

        // The run works on its own copy of the project config so that nothing a module writes at
        // runtime lands in the developer's file, and the exact config used is kept with the results.
        var configCopy = Path.Combine(runDirectory, "config.toml");
        File.Copy(options.ProjectConfigPath, configCopy);
        if (options.ScenarioPath is not null)
        {
            File.Copy(options.ScenarioPath, Path.Combine(runDirectory, "scenario.toml"));
        }

        // Configurables register from module initializers, which only run once something in that
        // assembly is touched. Run them now so the storages and overrides see every entry.
        foreach (var module in (ReadOnlySpan<System.Reflection.Module>)
                 [
                     typeof(Storage).Module, typeof(Vision.Runner).Module, typeof(Referee.Runner).Module,
                     typeof(Sender.Runner).Module, typeof(Soccer.Runner).Module, typeof(SimulatorChannel).Module,
                 ])
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(module.ModuleHandle);
        }

        using var projectConfigs = new Storage(configCopy, StorageType.Project);
        using var userConfigs = options.UserConfigPath is null
            ? null
            : new Storage(options.UserConfigPath, StorageType.User);

        try
        {
            ConfigOverrides.Apply(BuildOverrides(options, scenario, runDirectory, label));
        }
        catch (ArgumentException ex)
        {
            Log.ZLogError($"{ex.Message}");
            return ExitBadScenario;
        }

        try
        {
            return scenario is null
                ? RunHeadless(cancellation)
                : RunScenario(scenario, runDirectory, configCopy, cancellation);
        }
        catch (OperationCanceledException)
        {
            Log.ZLogInformation($"Cancelled.");
            return ExitOk;
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Harness failed");
            return ExitError;
        }
    }

    private static IEnumerable<KeyValuePair<string, TomlValue>> BuildOverrides(
        HarnessOptions options, Scenario? scenario, string runDirectory, string label)
    {
        var overrides = new List<KeyValuePair<string, TomlValue>>
        {
            Override("Common.Debug.Db.DebugDbDumper.RootDirectory", new TomlString(runDirectory)),
            Override("Common.Debug.Db.DebugDbDumper.CaptureLabel", new TomlString(label)),
        };

        if (scenario is { GrSim.Mode: not ProcessMode.None })
        {
            // A simulated run must never talk to real robots, and must listen to grSim's vision port.
            overrides.Add(Override("Vision.SslVisionDataPublisher.UseSimulator", TomlBoolean.True));
            overrides.Add(Override("Sender.Simulator.Enabled", TomlBoolean.True));
            overrides.Add(Override("Sender.Nrf.Enabled", TomlBoolean.False));
            overrides.Add(Override("Sender.ZmqRobotSender.Enabled", TomlBoolean.False));
        }

        if (scenario is not null)
        {
            overrides.AddRange(scenario.Config);
        }

        overrides.AddRange(ConfigOverrides.FromStrings(options.Overrides));
        return overrides;

        static KeyValuePair<string, TomlValue> Override(string key, TomlValue value) => new(key, value);
    }

    // ── plain headless mode ─────────────────────────────────────────────────

    private static int RunHeadless(CancellationToken cancellation)
    {
        using var dumper = new DebugDbDumper();

        using var sender = new Sender.Runner();
        using var referee = new Referee.Runner();
        using var vision = new Vision.Runner();
        using var soccer = new Soccer.Runner();

        using var sslVisionPublisher = new Vision.SslVisionDataPublisher();
        using var gcPublisher = new Referee.GcDataPublisher();
        using var robotStatusPublisher = new Sender.RobotStatusPublisher();
        using var robotDiscoveryPublisher = new Sender.RobotDiscoveryPublisher();

        Log.ZLogInformation($"Running headless until SIGINT/SIGTERM. Session: {dumper.SessionDirectory}");
        cancellation.WaitHandle.WaitOne();
        return ExitOk;
    }

    // ── scenario mode ───────────────────────────────────────────────────────

    private static int RunScenario(Scenario scenario, string runDirectory, string configPath, CancellationToken cancellation)
    {
        Log.ZLogInformation($"Scenario {scenario.Name}: {scenario.Description}");

        // 1. External processes first, so the stack finds them when it starts listening.
        using var grSim = scenario.GrSim.Mode == ProcessMode.None ? null : new GrSimProcess();
        var launchedGrSim = false;
        if (grSim is not null && !EnsureGrSim(grSim, scenario.GrSim, cancellation, out launchedGrSim))
        {
            return ExitNotReady;
        }

        using var gc = scenario.Gc.Mode == ProcessMode.None ? null : new GcProcess();
        using var gcApi = gc is null ? null : new GcApiClient();
        var launchedGc = false;
        if (gc is not null && !EnsureGc(gc, gcApi!, scenario.Gc, cancellation, out launchedGc))
        {
            return ExitNotReady;
        }

        try
        {
            // 2. The stack. The recorder goes first so every module's output is captured.
            using var dumper = new DebugDbDumper();
            using var sender = new Sender.Runner();
            using var referee = new Referee.Runner();
            using var vision = new Vision.Runner();
            using var soccer = new Soccer.Runner();
            using var sslVisionPublisher = new Vision.SslVisionDataPublisher();
            using var gcPublisher = new Referee.GcDataPublisher();

            using var sampler = new WorldSampler();
            using var sim = grSim is null ? null : new SimulatorChannel();

            // 3. Readiness: grSim must publish to the port we listen on, and frames must be flowing.
            if (sim is not null)
            {
                sim.SendConfig(visionPort: (uint)Vision.SslVisionDataPublisher.SimulatorVisionPort);

                if (!WaitUntil(() =>
                    {
                        sampler.Poll();
                        return sampler.FramesSeen >= 10;
                    }, scenario.ReadinessTimeout, "vision frames from grSim", cancellation))
                {
                    return ExitNotReady;
                }
            }

            if (gcApi is not null && !WaitUntil(() =>
                {
                    sampler.Poll();
                    return sampler.LatestReferee is not null;
                }, scenario.ReadinessTimeout, "referee packets from the game controller", cancellation))
            {
                return ExitNotReady;
            }

            // 4. Setup script, then the timed run.
            sampler.AddEvent("harness", "setup");
            foreach (var step in scenario.Setup)
            {
                cancellation.ThrowIfCancellationRequested();
                Execute(step, sim, gcApi, sampler);
            }

            sampler.AddEvent("harness", "run");
            sampler.Start(scenario.SampleHz);
            Log.ZLogInformation($"Running for {scenario.Duration.Seconds:F1} s at {scenario.SampleHz} Hz sampling");
            cancellation.WaitHandle.WaitOne(scenario.Duration.ToTimeSpan());
            sampler.Stop();

            // 5. Results.
            var summary = sampler.BuildSummary(scenario.Name, dumper.SessionDirectory, configPath, scenario.SampleHz);
            var summaryPath = Path.Combine(runDirectory, "summary.json");
            WorldSampler.WriteSummary(summary, summaryPath);
            Log.ZLogInformation($"Wrote {summaryPath} ({summary.Samples.Length} samples, {summary.Events.Length} events, {summary.VisionFrames} vision frames)");

            return ExitOk;
        }
        finally
        {
            gcApi?.Disconnect();
            if (launchedGc) gc?.Stop();
            if (launchedGrSim) grSim?.Stop();
        }
    }

    private static void Execute(ScenarioStep step, SimulatorChannel? sim, GcApiClient? gc, WorldSampler sampler)
    {
        const float mmPerMeter = 1000f;

        switch (step)
        {
            case TeleportBallStep ball:
                Require(sim, step).TeleportBall(new Common.Data.Ssl.Simulation.TeleportBall
                {
                    X = ball.X / mmPerMeter,
                    Y = ball.Y / mmPerMeter,
                    Z = ball.Z / mmPerMeter,
                    Vx = ball.Vx / mmPerMeter,
                    Vy = ball.Vy / mmPerMeter,
                });
                sampler.AddEvent("teleport_ball", $"({ball.X}, {ball.Y})");
                break;

            case TeleportRobotStep robot:
                Require(sim, step).TeleportRobot(robot.Team, robot.Id, robot.X / mmPerMeter, robot.Y / mmPerMeter, robot.Orientation);
                sampler.AddEvent("teleport_robot", $"{robot.Team} {robot.Id} -> ({robot.X}, {robot.Y})");
                break;

            case RemoveRobotStep remove:
                Require(sim, step).RemoveRobot(remove.Team, remove.Id);
                sampler.AddEvent("remove_robot", $"{remove.Team} {remove.Id}");
                break;

            case SimSpeedStep speed:
                Require(sim, step).SetSimulationSpeed(speed.Speed);
                sampler.AddEvent("sim_speed", $"{speed.Speed}");
                break;

            case GcCommandStep command:
                Require(gc, step).Send(GcInput.Command(command.Command, command.ForTeam));
                sampler.AddEvent("gc", $"{command.Command} {command.ForTeam}");
                break;

            case GcStageStep stage:
                Require(gc, step).Send(GcInput.ChangeStage(stage.Stage));
                sampler.AddEvent("gc_stage", stage.Stage);
                break;

            case GcBallPlacementPosStep pos:
                Require(gc, step).Send(GcInput.SetBallPlacementPos(pos.X / mmPerMeter, pos.Y / mmPerMeter));
                sampler.AddEvent("gc_ball_placement_pos", $"({pos.X}, {pos.Y})");
                break;

            case GcTeamSideStep side:
                Require(gc, step).Send(GcInput.SetTeamOnPositiveHalf(side.Team.ToString().ToUpperInvariant(), side.PositiveHalf));
                sampler.AddEvent("gc_side", $"{side.Team} on {(side.PositiveHalf ? "positive" : "negative")} half");
                break;

            case WaitStep wait:
                Thread.Sleep(wait.Duration.ToTimeSpan());
                break;

            default:
                throw new InvalidDataException($"Unhandled scenario step {step}");
        }

        Log.ZLogInformation($"Setup: {step}");
    }

    private static T Require<T>(T? dependency, ScenarioStep step) where T : class =>
        dependency ?? throw new InvalidDataException(
            $"Step {step.GetType().Name} needs {(typeof(T) == typeof(SimulatorChannel) ? "grSim" : "the game controller")}, " +
            "but the scenario has its process mode set to none");

    // ── external processes ─────────────────────────────────────────────────

    private static bool EnsureGrSim(GrSimProcess grSim, GrSimSpec spec, CancellationToken cancellation, out bool launched)
    {
        launched = false;
        grSim.Refresh();

        if (grSim.CurrentStatus == GrSimProcess.Status.Running)
        {
            Log.ZLogInformation($"grSim: using running instance ({grSim.StatusMessage})");
            return true;
        }

        if (spec.Mode == ProcessMode.Attach)
        {
            Log.ZLogError($"grSim is not running and the scenario asks to attach");
            return false;
        }

        if (grSim.CachedVersion is null)
        {
            Log.ZLogInformation($"grSim: no cached binary, downloading");
            grSim.StartDownload();
            if (!WaitUntil(() => grSim.CurrentStatus != GrSimProcess.Status.Downloading, DeltaTime.FromSeconds(300), "grSim download", cancellation))
                return false;
            if (grSim.CurrentStatus == GrSimProcess.Status.Error)
            {
                Log.ZLogError($"grSim download failed: {grSim.StatusMessage}");
                return false;
            }
        }

        grSim.Start(spec.Headless);
        launched = true;
        Log.ZLogInformation($"grSim: launched {(spec.Headless ? "headless" : "with UI")}");

        // Refresh() only rescans once a second, so poll it until the detached process is found.
        return WaitUntil(() =>
        {
            grSim.Refresh();
            return grSim.CurrentStatus == GrSimProcess.Status.Running;
        }, DeltaTime.FromSeconds(20), "grSim process", cancellation);
    }

    private static bool EnsureGc(GcProcess gc, GcApiClient api, GcSpec spec, CancellationToken cancellation, out bool launched)
    {
        launched = false;

        if (spec.Mode == ProcessMode.Launch && gc.CachedVersion is null)
        {
            Log.ZLogInformation($"game controller: no cached binary, downloading");
            gc.StartDownload();
            if (!WaitUntil(() => gc.CurrentStatus != GcProcess.Status.Downloading, DeltaTime.FromSeconds(300), "game controller download", cancellation))
                return false;
        }

        // Start() attaches to a running GC before launching a new one, so both modes go through it;
        // in attach mode we only refuse to be the one who launched.
        launched = gc.Start(spec.RconPort, spec.UiPort, Referee.GcDataPublisher.GcAddress);
        if (gc.CurrentStatus != GcProcess.Status.Running)
        {
            Log.ZLogError($"game controller not running: {gc.StatusMessage}");
            return false;
        }

        if (launched && spec.Mode == ProcessMode.Attach)
        {
            Log.ZLogWarning($"game controller: scenario asked to attach but none was running; launched one");
        }

        Log.ZLogInformation($"game controller: {gc.StatusMessage}");

        api.Connect("localhost", spec.UiPort);
        return WaitUntil(() => api.State == GcApiClient.ConnectionState.Connected && api.MatchState is not null,
            DeltaTime.FromSeconds(20), "game controller API connection", cancellation);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static bool WaitUntil(Func<bool> condition, DeltaTime timeout, string what, CancellationToken cancellation)
    {
        var deadline = DateTime.UtcNow + timeout.ToTimeSpan();
        while (!condition())
        {
            cancellation.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
            {
                Log.ZLogError($"Timed out after {timeout.Seconds:F0} s waiting for {what}");
                return false;
            }

            Thread.Sleep(PollInterval);
        }

        Log.ZLogInformation($"Ready: {what}");
        return true;
    }

    private static string CreateRunDirectory(string root, string label)
    {
        var directory = Path.Combine(Path.GetFullPath(root), $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{label}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}

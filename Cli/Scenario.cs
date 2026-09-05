using Tomlet;
using Tomlet.Models;
using Tyr.Common.Data;
using Tyr.Common.Math;
using Tyr.Common.Time;

namespace Tyr.Cli;

public enum ProcessMode
{
    /// <summary>Do not start or look for the process; assume nothing about it.</summary>
    None,

    /// <summary>Use an already running instance; fail if none is found.</summary>
    Attach,

    /// <summary>Use a running instance if there is one, otherwise download (if needed) and launch it.</summary>
    Launch,
}

public sealed record GrSimSpec(ProcessMode Mode, bool Headless);

public sealed record GcSpec(ProcessMode Mode, int RconPort, int UiPort);

public abstract record ScenarioStep;

/// <summary>Positions in mm, velocities in mm/s (repo convention); converted to meters at the grSim boundary.</summary>
public sealed record TeleportBallStep(float X, float Y, float Z, float Vx, float Vy) : ScenarioStep;

public sealed record TeleportRobotStep(TeamColor Team, uint Id, float X, float Y, Angle? Orientation) : ScenarioStep;

public sealed record RemoveRobotStep(TeamColor Team, uint Id) : ScenarioStep;

/// <summary>A game-controller command, e.g. STOP, FORCE_START, NORMAL_START, KICKOFF, DIRECT, PENALTY, BALL_PLACEMENT, HALT.</summary>
public sealed record GcCommandStep(string Command, string ForTeam) : ScenarioStep;

public sealed record GcStageStep(string Stage) : ScenarioStep;

/// <summary>Designated ball placement position in mm.</summary>
public sealed record GcBallPlacementPosStep(float X, float Y) : ScenarioStep;

/// <summary>Which half a team defends. Scenarios should set this explicitly instead of relying on GC defaults.</summary>
public sealed record GcTeamSideStep(TeamColor Team, bool PositiveHalf) : ScenarioStep;

public sealed record WaitStep(DeltaTime Duration) : ScenarioStep;

public sealed record SimSpeedStep(float Speed) : ScenarioStep;

/// <summary>
/// A scripted simulation run: which processes to use, config overrides, an ordered setup script,
/// and how long to run while sampling the world. Loaded from TOML, see Data/scenarios/*.toml.
/// </summary>
public sealed class Scenario
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public GrSimSpec GrSim { get; init; } = new(ProcessMode.Launch, Headless: true);
    public GcSpec Gc { get; init; } = new(ProcessMode.Launch, RconPort: 10011, UiPort: 8081);
    public IReadOnlyDictionary<string, TomlValue> Config { get; init; } = new Dictionary<string, TomlValue>();
    public IReadOnlyList<ScenarioStep> Setup { get; init; } = [];
    public DeltaTime Duration { get; init; } = DeltaTime.FromSeconds(10);
    public float SampleHz { get; init; } = 10f;

    /// <summary>Time to wait for vision frames after grSim is up before the setup script runs.</summary>
    public DeltaTime ReadinessTimeout { get; init; } = DeltaTime.FromSeconds(20);

    public static Scenario Load(string path)
    {
        var document = TomlParser.ParseFile(path);

        var name = Get<string>(document, "name") ?? Path.GetFileNameWithoutExtension(path);

        var grSimMode = ParseMode(Get<string>(document, "processes.grsim.mode"), ProcessMode.Launch);
        var grSimHeadless = Get<bool?>(document, "processes.grsim.headless") ?? true;

        var gcMode = ParseMode(Get<string>(document, "processes.gc.mode"), ProcessMode.Launch);
        var rconPort = (int)(Get<long?>(document, "processes.gc.rcon_port") ?? 10011);
        var uiPort = (int)(Get<long?>(document, "processes.gc.ui_port") ?? 8081);

        var config = new Dictionary<string, TomlValue>();
        if (document.TryGetValue("config", out var configValue) && configValue is TomlTable configTable)
        {
            foreach (var (key, value) in configTable.Entries)
            {
                config[key] = value;
            }
        }

        var steps = new List<ScenarioStep>();
        if (document.TryGetValue("setup", out var setupValue) && setupValue is TomlArray setupArray)
        {
            foreach (var item in setupArray.ArrayValues)
            {
                if (item is not TomlTable step)
                    throw new InvalidDataException($"{path}: every [[setup]] entry must be a table");
                steps.Add(ParseStep(step, path));
            }
        }

        var durationSeconds = Get<double?>(document, "run.duration_seconds") ?? 10.0;
        var sampleHz = (float)(Get<double?>(document, "run.sample_hz") ?? 10.0);
        var readinessSeconds = Get<double?>(document, "run.readiness_timeout_seconds") ?? 20.0;

        return new Scenario
        {
            Name = name,
            Description = Get<string>(document, "description") ?? "",
            GrSim = new GrSimSpec(grSimMode, grSimHeadless),
            Gc = new GcSpec(gcMode, rconPort, uiPort),
            Config = config,
            Setup = steps,
            Duration = DeltaTime.FromSeconds((float)durationSeconds),
            SampleHz = sampleHz,
            ReadinessTimeout = DeltaTime.FromSeconds((float)readinessSeconds),
        };
    }

    private static ScenarioStep ParseStep(TomlTable step, string path)
    {
        var action = Get<string>(step, "action") ?? throw new InvalidDataException($"{path}: [[setup]] entry without 'action'");

        return action switch
        {
            "teleport_ball" => new TeleportBallStep(
                Number(step, "x"), Number(step, "y"), Number(step, "z", 0f),
                Number(step, "vx", 0f), Number(step, "vy", 0f)),

            "teleport_robot" => new TeleportRobotStep(
                Team(step), (uint)(Get<long?>(step, "id") ?? throw Missing(path, action, "id")),
                Number(step, "x"), Number(step, "y"),
                Get<double?>(step, "orientation_deg") is { } deg ? Angle.FromDeg((float)deg) : null),

            "remove_robot" => new RemoveRobotStep(Team(step), (uint)(Get<long?>(step, "id") ?? throw Missing(path, action, "id"))),

            "gc" => new GcCommandStep(
                (Get<string>(step, "command") ?? throw Missing(path, action, "command")).ToUpperInvariant(),
                (Get<string>(step, "for_team") ?? "unknown").ToUpperInvariant()),

            "gc_stage" => new GcStageStep((Get<string>(step, "stage") ?? throw Missing(path, action, "stage")).ToUpperInvariant()),

            "gc_ball_placement_pos" => new GcBallPlacementPosStep(Number(step, "x"), Number(step, "y")),

            "gc_side" => new GcTeamSideStep(Team(step),
                Get<bool?>(step, "positive_half") ?? throw Missing(path, action, "positive_half")),

            "wait" => new WaitStep(DeltaTime.FromSeconds(Number(step, "seconds"))),

            "sim_speed" => new SimSpeedStep(Number(step, "speed")),

            _ => throw new InvalidDataException($"{path}: unknown setup action '{action}'"),
        };

        static Exception Missing(string path, string action, string field) =>
            new InvalidDataException($"{path}: setup action '{action}' needs '{field}'");
    }

    private static TeamColor Team(TomlTable step)
    {
        var team = Get<string>(step, "team")?.ToLowerInvariant();
        return team switch
        {
            "yellow" => TeamColor.Yellow,
            "blue" => TeamColor.Blue,
            _ => throw new InvalidDataException($"setup step needs team = \"yellow\" | \"blue\", got '{team}'"),
        };
    }

    private static float Number(TomlTable table, string key, float? fallback = null)
    {
        if (!table.TryGetValue(key, out var value))
        {
            return fallback ?? throw new InvalidDataException($"setup step needs '{key}'");
        }

        return value switch
        {
            TomlLong l => l.Value,
            TomlDouble d => (float)d.Value,
            _ => throw new InvalidDataException($"'{key}' must be a number"),
        };
    }

    private static ProcessMode ParseMode(string? value, ProcessMode fallback) => value?.ToLowerInvariant() switch
    {
        null => fallback,
        "none" => ProcessMode.None,
        "attach" => ProcessMode.Attach,
        "launch" => ProcessMode.Launch,
        _ => throw new InvalidDataException($"process mode must be none | attach | launch, got '{value}'"),
    };

    private static T? Get<T>(TomlTable table, string dottedKey)
    {
        if (!table.TryGetValue(dottedKey, out var value)) return default;
        return TomletMain.To<T>(value);
    }
}

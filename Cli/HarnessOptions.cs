namespace Tyr.Cli;

/// <summary>Command line of the headless runner. See <see cref="Usage"/>.</summary>
public sealed class HarnessOptions
{
    public const string Usage = """
        Tyr.Cli <project-config.toml> [options]

        Runs the full Tyr stack headless (vision, referee, soccer, sender, debug-db recorder).
        Without --scenario it runs until SIGINT/SIGTERM, like the GUI without the GUI.

        Options:
          --scenario <file.toml>   Drive grSim + game controller through a scenario and write results.
          --set <Path.Entry=value> Override a config entry (TOML value syntax), e.g. --set Soccer.Runner.RunYellow=true.
                                   May be repeated. Applied after the config files, before any module starts.
          --user <user.toml>       Also load a user config file. Off by default so runs are reproducible.
          --out <dir>              Root directory for run outputs (default: ./runs).
          --duration <seconds>     Override the scenario's run duration.
          --help                   Show this text.
        """;

    public required string ProjectConfigPath { get; init; }
    public string? ScenarioPath { get; init; }
    public string? UserConfigPath { get; init; }
    public string OutputRoot { get; init; } = "runs";
    public double? DurationSecondsOverride { get; init; }
    public IReadOnlyList<KeyValuePair<string, string>> Overrides { get; init; } = [];
    public bool ShowHelp { get; init; }

    public static HarnessOptions Parse(string[] args)
    {
        string? project = null;
        string? scenario = null;
        string? user = null;
        string output = "runs";
        double? duration = null;
        var overrides = new List<KeyValuePair<string, string>>();
        var help = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help" or "-h":
                    help = true;
                    break;
                case "--scenario":
                    scenario = Next(args, ref i, arg);
                    break;
                case "--user":
                    user = Next(args, ref i, arg);
                    break;
                case "--out":
                    output = Next(args, ref i, arg);
                    break;
                case "--duration":
                    duration = double.Parse(Next(args, ref i, arg), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--set":
                {
                    var assignment = Next(args, ref i, arg);
                    var eq = assignment.IndexOf('=');
                    if (eq <= 0) throw new ArgumentException($"--set expects Path.Entry=value, got '{assignment}'");
                    overrides.Add(new KeyValuePair<string, string>(assignment[..eq].Trim(), assignment[(eq + 1)..].Trim()));
                    break;
                }
                default:
                    if (arg.StartsWith('-')) throw new ArgumentException($"Unknown option '{arg}'");
                    if (project is not null) throw new ArgumentException($"Unexpected positional argument '{arg}'");
                    project = arg;
                    break;
            }
        }

        if (help)
        {
            return new HarnessOptions { ProjectConfigPath = project ?? "", ShowHelp = true };
        }

        if (project is null) throw new ArgumentException("Missing <project-config.toml>");

        return new HarnessOptions
        {
            ProjectConfigPath = project,
            ScenarioPath = scenario,
            UserConfigPath = user,
            OutputRoot = output,
            DurationSecondsOverride = duration,
            Overrides = overrides,
        };
    }

    private static string Next(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"{option} needs a value");
        return args[++i];
    }
}
